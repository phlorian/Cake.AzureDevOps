﻿namespace Cake.AzureDevOps
{
    using System;
    using Cake.AzureDevOps.Authentication;
    using Microsoft.TeamFoundation.SourceControl.WebApi;
    using Microsoft.VisualStudio.Services.Identity;
    using Microsoft.VisualStudio.Services.WebApi;

    /// <inheritdoc />
    internal class GitClientFactory : IGitClientFactory
    {
        /// <summary>
        /// Creates a client object for communicating with Azure DevOps.
        /// </summary>
        /// <param name="collectionUrl">The URL of the Azure DevOps team project collection.</param>
        /// <param name="credentials">The credentials to connect to Azure DevOps.</param>
        /// <param name="authorizedIdentity">Returns identity which is authorized.</param>
        /// <returns>Client object for communicating with Azure DevOps.</returns>
        public GitHttpClient CreateGitClient(Uri collectionUrl, IAzureDevOpsCredentials credentials, out Identity authorizedIdentity)
        {
            var connection =
                new VssConnection(collectionUrl, credentials.ToVssCredentials());

            authorizedIdentity = connection.AuthorizedIdentity;

            var gitClient = connection.GetClient<GitHttpClient>();
            if (gitClient == null)
            {
                throw new AzureDevOpsException("Could not retrieve the GitHttpClient object");
            }

            return gitClient;
        }

        /// <summary>
        /// Creates a client object for communicating with Azure DevOps.
        /// </summary>
        /// <param name="collectionUrl">The URL of the Azure DevOps team project collection.</param>
        /// <param name="credentials">The credentials to connect to Azure DevOps.</param>
        /// <returns>Client object for communicating with Azure DevOps.</returns>
        public GitHttpClient CreateGitClient(Uri collectionUrl, IAzureDevOpsCredentials credentials)
        {
            return this.CreateGitClient(collectionUrl, credentials, out _);
        }
    }
}
