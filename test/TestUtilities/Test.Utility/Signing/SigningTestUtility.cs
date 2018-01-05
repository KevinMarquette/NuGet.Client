// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace Test.Utility.Signing
{
    public static class SigningTestUtility
    {
        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to ClientAuth.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorForInvalidEkuCert = delegate (X509V3CertificateGenerator gen)
        {
            // any EKU besides CodeSigning
            var usages = new[] { KeyPurposeID.IdKPClientAuth };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to CodeSigning.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorForCodeSigningEkuCert = delegate (X509V3CertificateGenerator gen)
        {
            // CodeSigning EKU
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create an expired certificate.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorExpiredCert = delegate (X509V3CertificateGenerator gen)
        {
            // CodeSigning EKU
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            gen.SetNotBefore(DateTime.Now.AddHours(-1));
            gen.SetNotAfter(DateTime.Now.AddMinutes(-1));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is not yet valid.
        /// </summary>
        public static Action<X509V3CertificateGenerator> CertificateModificationGeneratorNotYetValidCert = delegate (X509V3CertificateGenerator gen)
        {
            // CodeSigning EKU
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            var notBefore = DateTime.Now.AddDays(1);
            var notAfter = notBefore.AddHours(1);

            gen.SetNotBefore(notBefore);
            gen.SetNotAfter(notAfter);
        };

        /// <summary>
        /// Generates a list of certificates representing a chain of certificates.
        /// The first certificate is the root certificate stored in StoreName.Root and StoreLocation.LocalMachine.
        /// The last certificate is the leaf certificate stored in StoreName.TrustedPeople and StoreLocation.LocalMachine.
        /// Please dispose all the certificates in the list after use.
        /// </summary>
        /// <param name="length">Length of the chain.</param>
        /// <returns>List of certificates representing a chain of certificates.</returns>
        public static IList<TrustedTestCert<TestCertificate>> GenerateCertificateChain(int length, string crlServerUri, string crlLocalUri)
        {
            var certChain = new List<TrustedTestCert<TestCertificate>>();
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;
            TrustedTestCert<TestCertificate> issuer = null;
            TrustedTestCert<TestCertificate> cert = null;

            for (var i = 0; i < length; i++)
            {
                if (i == 0) // root CA cert
                {
                    cert = TestCertificate.Generate(actionGenerator, isCA: true, crlServerUri: crlServerUri, crlLocalUri: crlLocalUri).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine);
                    issuer = cert;
                }
                else if (i < length - 1) // intermediate CA cert
                {
                    cert = TestCertificate.Generate(actionGenerator, issuer.Source.Cert, isCA: true, crlServerUri: crlServerUri, crlLocalUri: crlLocalUri).WithPrivateKeyAndTrust(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                    issuer = cert;
                }
                else // leaf cert
                {
                    cert = TestCertificate.Generate(actionGenerator, issuer.Source.Cert, crlServerUri: crlServerUri).WithPrivateKeyAndTrust(StoreName.My, StoreLocation.LocalMachine);
                }

                certChain.Add(cert);
            }

            return certChain;
        }

        public static void RevokeCertificate(TrustedTestCert<TestCertificate> revokeCertificate, TrustedTestCert<TestCertificate> RevocationAuthorityCertificate)
        {
            if (RevocationAuthorityCertificate.Source.Crl == null)
            {
                throw new InvalidOperationException("Revoking authority must have a valid CRL");
            }

            var crl = RevocationAuthorityCertificate.Source.Crl;

        }

        /// <summary>
        /// Create a self signed certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<X509V3CertificateGenerator> modifyGenerator,
            string signatureAlgorithm = "SHA256WITHRSA",
            int publicKeyLength = 2048,
            X509Certificate2 issuer = null,
            bool isCA = false,
            string crlServerUri = null)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var random = new SecureRandom();
            var pairGenerator = new RsaKeyPairGenerator();
            var genParams = new KeyGenerationParameters(random, publicKeyLength);
            pairGenerator.Init(genParams);
            var issuerKeyPair = pairGenerator.GenerateKeyPair();

            // Create cert
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name($"CN={subjectName}"));

            // default to new key pair
            var issuerPrivateKey = issuerKeyPair.Private;
            var keyUsage = KeyUsage.DigitalSignature;

#if IS_DESKTOP
            if (issuer == null)
            {
                certGen.SetIssuerDN(new X509Name($"CN={subjectName}"));
            }
            else
            {
                var bcIssuer = DotNetUtilities.FromX509Certificate(issuer);
                var authorityKeyIdentifier = new AuthorityKeyIdentifierStructure(bcIssuer);
                issuerPrivateKey = DotNetUtilities.GetKeyPair(issuer.PrivateKey).Private;
                certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifier);
                certGen.SetIssuerDN(bcIssuer.SubjectDN);
            }
#else
            certGen.SetIssuerDN(new X509Name($"CN={subjectName}"));
#endif

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(issuerKeyPair.Public);

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(issuerKeyPair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);

            if (isCA)
            {
                keyUsage = KeyUsage.CrlSign | KeyUsage.KeyCertSign | KeyUsage.DigitalSignature;

                var generalNames = new GeneralNames(new GeneralName(GeneralName.UniformResourceIdentifier, new DerIA5String($"{crlServerUri}CN={subjectName}.crl")));
                var distPointName = new DistributionPointName(generalNames);
                var distPoint = new DistributionPoint(distPointName, null, null);

                certGen.AddExtension(X509Extensions.CrlDistributionPoints, critical: false, extensionValue: new DerSequence(distPoint));
            }
            else if (issuer != null)
            {
                var generalNames = new GeneralNames(new GeneralName(GeneralName.UniformResourceIdentifier, new DerIA5String($"{crlServerUri}{issuer.Subject}.crl")));
                var distPointName = new DistributionPointName(generalNames);
                var distPoint = new DistributionPoint(distPointName, null, null);

                certGen.AddExtension(X509Extensions.CrlDistributionPoints, critical: false, extensionValue: new DerSequence(distPoint));
            }

            certGen.AddExtension(X509Extensions.KeyUsage.Id, false, new KeyUsage(keyUsage));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(isCA));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

#if IS_DESKTOP
            certResult.PrivateKey = DotNetUtilities.ToRSA(issuerKeyPair.Private as RsaPrivateCrtKeyParameters);
#endif

            return certResult;
        }

        private static X509SubjectKeyIdentifierExtension GetSubjectKeyIdentifier(X509Certificate2 issuer)
        {
            var subjectKeyIdentifierOid = "2.5.29.14";

            foreach (var extension in issuer.Extensions)
            {
                if (string.Equals(extension.Oid.Value, subjectKeyIdentifierOid))
                {
                    return extension as X509SubjectKeyIdentifierExtension;
                }
            }

            return null;
        }

#if IS_DESKTOP
        /// <summary>
        /// Convert a cert private key into a AsymmetricKeyParameter
        /// </summary>
        public static AsymmetricKeyParameter GetPrivateKeyParameter(X509Certificate2 cert)
        {
            return DotNetUtilities.GetKeyPair(cert.PrivateKey).Private;
        }

        /// <summary>
        /// Generates a SignedCMS object for some content.
        /// </summary>
        /// <param name="content"></param>
        /// <returns>SignedCms object</returns>
        public static SignedCms GenerateSignedCms(X509Certificate2 cert, byte[] content)
        {
            var contentInfo = new ContentInfo(content);
            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            return cms;
        }

#endif

        /// <summary>
        /// Returns the public cert without the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCert(X509Certificate2 cert)
        {
            return new X509Certificate2(cert.Export(X509ContentType.Cert));
        }

        /// <summary>
        /// Returns the public cert with the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCertWithPrivateKey(X509Certificate2 cert)
        {
            var pass = new Guid().ToString();
            return new X509Certificate2(cert.Export(X509ContentType.Pfx, pass), pass, X509KeyStorageFlags.PersistKeySet);
        }
    }
}