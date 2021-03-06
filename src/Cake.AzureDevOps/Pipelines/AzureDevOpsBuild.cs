﻿namespace Cake.AzureDevOps.Pipelines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cake.AzureDevOps.Authentication;
    using Cake.Core.Diagnostics;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.TeamFoundation.TestManagement.WebApi;

    /// <summary>
    /// Class for writing issues to Azure DevOps pull requests.
    /// </summary>
    public sealed class AzureDevOpsBuild
    {
        private readonly ICakeLog log;
        private readonly IAzureDevOpsCredentials credentials;
        private readonly bool throwExceptionIfBuildCouldNotBeFound;
        private readonly IBuildClientFactory buildClientFactory;
        private readonly ITestManagementClientFactory testClientFactory;
        private readonly Build build;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureDevOpsBuild"/> class.
        /// </summary>
        /// <param name="log">The Cake log context.</param>
        /// <param name="settings">Settings for accessing AzureDevOps.</param>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/>
        /// is set to <c>true</c> and no build could be found.</exception>
        public AzureDevOpsBuild(ICakeLog log, AzureDevOpsBuildSettings settings)
            : this(log, settings, new BuildClientFactory())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureDevOpsBuild"/> class.
        /// </summary>
        /// <param name="log">The Cake log context.</param>
        /// <param name="settings">Settings for accessing AzureDevOps.</param>
        /// <param name="build">The build.</param>
        internal AzureDevOpsBuild(ICakeLog log, AzureDevOpsBuildsSettings settings, Build build)
        {
            log.NotNull(nameof(log));
            settings.NotNull(nameof(settings));
            build.NotNull(nameof(build));

            this.log = log;
            this.build = build;
            this.buildClientFactory = new BuildClientFactory();
            this.testClientFactory = new TestManagementClientFactory();
            this.credentials = settings.Credentials;
            this.CollectionUrl = settings.CollectionUrl;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureDevOpsBuild"/> class.
        /// </summary>
        /// <param name="log">The Cake log context.</param>
        /// <param name="settings">Settings for accessing AzureDevOps.</param>
        /// <param name="buildClientFactory">A factory to communicate with Build client.</param>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/>
        /// is set to <c>true</c> and no build could be found.</exception>
        internal AzureDevOpsBuild(ICakeLog log, AzureDevOpsBuildSettings settings, IBuildClientFactory buildClientFactory)
        {
            log.NotNull(nameof(log));
            settings.NotNull(nameof(settings));
            buildClientFactory.NotNull(nameof(buildClientFactory));

            this.log = log;
            this.buildClientFactory = buildClientFactory;
            this.testClientFactory = new TestManagementClientFactory();
            this.credentials = settings.Credentials;
            this.CollectionUrl = settings.CollectionUrl;
            this.throwExceptionIfBuildCouldNotBeFound = settings.ThrowExceptionIfBuildCouldNotBeFound;

            using (var buildClient = this.buildClientFactory.CreateBuildClient(settings.CollectionUrl, settings.Credentials, out var authorizedIdenity))
            {
                this.log.Verbose(
                     "Authorized Identity:\n  Id: {0}\n  DisplayName: {1}",
                     authorizedIdenity.Id,
                     authorizedIdenity.DisplayName);

                try
                {
                    if (settings.ProjectGuid != Guid.Empty)
                    {
                        this.log.Verbose("Read build with ID {0} from project with ID {1}", settings.BuildId, settings.ProjectGuid);
                        this.build =
                            buildClient
                                .GetBuildAsync(
                                    settings.ProjectGuid,
                                    settings.BuildId)
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();
                    }
                    else if (!string.IsNullOrWhiteSpace(settings.ProjectName))
                    {
                        this.log.Verbose("Read build with ID {0} from project with name {1}", settings.BuildId, settings.ProjectName);
                        this.build =
                            buildClient
                                .GetBuildAsync(
                                    settings.ProjectName,
                                    settings.BuildId)
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(settings),
                            "Either ProjectGuid or ProjectName needs to be set");
                    }
                }
                catch (BuildNotFoundException ex)
                {
                    if (this.throwExceptionIfBuildCouldNotBeFound)
                    {
                        throw new AzureDevOpsBuildNotFoundException("Build not found", ex);
                    }

                    this.log.Warning("Could not find build");
                    return;
                }
            }

            this.log.Verbose(
                "Build information:\n  Id: {0}\n  BuildNumber: {1}",
                this.build.Id,
                this.build.BuildNumber);
        }

        /// <summary>
        /// Gets a value indicating whether a build has been successfully loaded.
        /// </summary>
        public bool HasBuildLoaded => this.build != null;

        /// <summary>
        /// Gets the URL for accessing the web portal of the Azure DevOps collection.
        /// </summary>
        public Uri CollectionUrl { get; }

        /// <summary>
        /// Gets the id of the Azure DevOps project.
        /// Returns <see cref="Guid.Empty"/> if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public Guid ProjectId
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return Guid.Empty;
                }

                return this.build.Project.Id;
            }
        }

        /// <summary>
        /// Gets the name of the Azure DevOps project.
        /// Returns empty string if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public string ProjectName
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return string.Empty;
                }

                return this.build.Project.Name;
            }
        }

        /// <summary>
        /// Gets the name of the Git repository.
        /// Returns <see cref="string.Empty"/> if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public string RepositoryName
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return string.Empty;
                }

                return this.build.Repository.Name;
            }
        }

        /// <summary>
        /// Gets the ID of the repository.
        /// Returns <see cref="Guid.Empty"/> if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public Guid RepositoryId
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return Guid.Empty;
                }

                return new Guid(this.build.Repository.Id);
            }
        }

        /// <summary>
        /// Gets the ID of the build.
        /// Returns 0 if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public int BuildId
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return 0;
                }

                return this.build.Id;
            }
        }

        /// <summary>
        /// Gets the status of the build.
        /// Returns 0 if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public AzureDevOpsBuildStatus? Status
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return 0;
                }

                return this.build.Status?.ToAzureDevOpsBuildStatus();
            }
        }

        /// <summary>
        /// Gets the result of the build.
        /// Returns 0 if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Result is only available after a build has been finished.
        /// The check if a running build is failing you can call <see cref="IsBuildFailing"/>.
        /// </remarks>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public AzureDevOpsBuildResult? Result
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return 0;
                }

                return this.build.Result?.ToAzureDevOpsBuildResult();
            }
        }

        /// <summary>
        /// Gets the parameters passed to the build.
        /// Returns an empty dictionary if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.
        /// </summary>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public IDictionary<string, string> Parameters
        {
            get
            {
                if (!this.ValidateBuild())
                {
                    return new Dictionary<string, string>();
                }

                // API returns a JSON string, which we parse into a dictionary.
                return
                    this.build.Parameters
                        .Replace("{", string.Empty)
                        .Replace("}", string.Empty)
                        .Split(',')
                        .ToDictionary(
                            x => x.Split(':').First().Trim('"'),
                            x => x.Split(':').Last().Trim('"'));
            }
        }

        /// <summary>
        /// Gets the changes associated with a build.
        /// </summary>
        /// <returns>The changes associated with a build or an empty list if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public IEnumerable<AzureDevOpsChange> GetChanges()
        {
            if (!this.ValidateBuild())
            {
                return new List<AzureDevOpsChange>();
            }

            using (var buildClient = this.buildClientFactory.CreateBuildClient(this.CollectionUrl, this.credentials))
            {
                return
                    buildClient
                        .GetBuildChangesAsync(this.ProjectId, this.BuildId)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult()
                        .Select(x => x.ToAzureDevOpsChange());
            }
        }

        /// <summary>
        /// Checks if the build is failing.
        /// </summary>
        /// <returns><c>true</c> if build is failing.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public bool IsBuildFailing()
        {
            return
                this.ValidateBuild() &&
                this.GetTimelineRecords().Any(x => x.Result.HasValue && x.Result.Value == AzureDevOpsTaskResult.Failed);
        }

        /// <summary>
        /// Gets the timeline entries for a build.
        /// </summary>
        /// <returns>The timeline entries for the build or an empty list if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public IEnumerable<AzureDevOpsTimelineRecord> GetTimelineRecords()
        {
            if (!this.ValidateBuild())
            {
                return new List<AzureDevOpsTimelineRecord>();
            }

            using (var buildClient = this.buildClientFactory.CreateBuildClient(this.CollectionUrl, this.credentials))
            {
                return
                    buildClient
                        .GetBuildTimelineAsync(this.ProjectId, this.BuildId)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult()
                        .Records
                        .Select(x => x.ToAzureDevOpsTimelineRecord());
            }
        }

        /// <summary>
        /// Gets the artifacts associated with a build.
        /// </summary>
        /// <returns>The artifacts associated with a build or an empty list if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        public IEnumerable<AzureDevOpsBuildArtifact> GetArtifacts()
        {
            if (!this.ValidateBuild())
            {
                return new List<AzureDevOpsBuildArtifact>();
            }

            using (var buildClient = this.buildClientFactory.CreateBuildClient(this.CollectionUrl, this.credentials))
            {
                return
                    buildClient
                        .GetArtifactsAsync(this.ProjectId, this.BuildId)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult()
                        .Select(x => x.ToAzureDevOpsBuildArtifact());
            }
        }

        /// <summary>
        /// Gets the test run ID's of a build.
        /// </summary>
        /// <returns>A list of RunIDs as int.</returns>
        public IEnumerable<AzureDevOpsTestRun> GetTestRuns()
        {
            if (!this.ValidateBuild())
            {
                return new List<AzureDevOpsTestRun>();
            }

            using (var testClient = this.testClientFactory.CreateTestManagementClient(this.CollectionUrl, this.credentials.ToVssCredentials()))
            {
                return
                    testClient
                    .GetTestResultDetailsForBuildAsync(this.ProjectId, this.build.Id)
                    .GetAwaiter()
                    .GetResult()
                    .ResultsForGroup
                        .SelectMany(testResultGroup => testResultGroup.Results).ToList()
                        .GroupBy(testResult => testResult.TestRun.Id)
                        .Select(runGroup => int.Parse(runGroup.Key))
                        .Select(runId =>
                            new AzureDevOpsTestRun
                            {
                                RunId = runId,
                                TestResults =
                                    this.GetTestResults(testClient, runId)
                                    .GetAwaiter()
                                    .GetResult(),
                            });
            }
        }

        /// <summary>
        /// Gets the test runs of this build.
        /// </summary>
        /// <param name="testClient">Instance of a <see cref="TestManagementHttpClient"/> class.</param>
        /// <param name="runId">Id of the test run.</param>
        /// <returns>The test results for a build or an empty list if no build could be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>false</c>.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If build could not be found and
        /// <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/> is set to <c>true</c>.</exception>
        private async Task<IEnumerable<AzureDevOpsTestResult>> GetTestResults(TestManagementHttpClient testClient, int runId)
        {
            return
                (await testClient.GetTestResultsAsync(this.ProjectId, runId).ConfigureAwait(false))
                .Select(test =>
                    new AzureDevOpsTestResult
                    {
                        AutomatedTestName = test.AutomatedTestName,
                        Outcome = test.Outcome,
                        ErrorMessage = test.ErrorMessage,
                    });
        }

        /// <summary>
        /// Validates if a build could be found.
        /// Depending on <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/>
        /// the build instance can be null for subsequent calls.
        /// </summary>
        /// <returns>True if a valid build instance exists.</returns>
        /// <exception cref="AzureDevOpsBuildNotFoundException">If <see cref="AzureDevOpsBuildSettings.ThrowExceptionIfBuildCouldNotBeFound"/>
        /// is set to <c>true</c> and no build could be found.</exception>
        private bool ValidateBuild()
        {
            if (this.HasBuildLoaded)
            {
                return true;
            }

            if (this.throwExceptionIfBuildCouldNotBeFound)
            {
                throw new AzureDevOpsBuildNotFoundException("Build not found");
            }

            this.log.Verbose("Skipping, since no build instance could be found.");
            return false;
        }
    }
}
