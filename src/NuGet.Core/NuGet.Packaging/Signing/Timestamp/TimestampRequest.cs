// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Request for timestamping a signature
    /// </summary>
    public class TimestampRequest
    {
        /// <summary>
        /// Signature that needs to be timestamped.
        /// </summary>
        public Signature Signature { get; set; }

        /// <summary>
        /// Hash algorithm to be used for timestamping.
        /// </summary>
        public HashAlgorithmName TimestampHashAlgorithm { get; set; }
    }
}