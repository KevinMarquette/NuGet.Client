﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Data;
using NuGet.Versioning;

namespace NuGet.Client
{
    public class V3ReportAbuseResource : INuGetResource
    {
        private IEnumerable<Uri> _reportAbuseTemplates;

        public V3ReportAbuseResource(IEnumerable<Uri> reportAbuseTemplates)
        {
            if (reportAbuseTemplates == null || !reportAbuseTemplates.Any())
            {
                throw new ArgumentNullException("reportAbuseTemplates");
            }

            _reportAbuseTemplates = reportAbuseTemplates;
        }

        /// <summary>
        /// Gets a URL for reporting package abuse. The URL will not be verified to exist.
        /// </summary>
        /// <param name="id">The package id (natural casing)</param>
        /// <param name="version">The package version</param>
        /// <returns>The first URL from the resource, with the URI template applied.</returns>
        public Uri GetReportAbuseUrl(string id, NuGetVersion version)
        {
            return Utility.ApplyPackageIdVersionToUriTemplate(_reportAbuseTemplates.First(), id, version);
        }

        /// <summary>
        /// Gets a URL for reporting package abuse. The URL will be tested for success with a HEAD request.
        /// </summary>
        /// <param name="id">The package id (natural casing)</param>
        /// <param name="version">The package version</param>
        /// <param name="cancellationToken">The cancellation token to terminate HTTP requests</param>
        /// <returns>The first URL available from the resource, with the URI template applied.</returns>
        public async Task<Uri> GetReportAbuseUrl(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            HttpClient http = new HttpClient();

            foreach (Uri uri in Utility.ApplyPackageIdVersionToUriTemplate(_reportAbuseTemplates, id, version))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, uri);
                    HttpResponseMessage response = await http.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        return uri;
                    }
                }
            }

            return null;
        }
    }
}
