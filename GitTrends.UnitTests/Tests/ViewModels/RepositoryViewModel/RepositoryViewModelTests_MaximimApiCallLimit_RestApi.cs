﻿using System.Net.Http;
using System.Net;
using System;
using System.Threading.Tasks;
using GitTrends.Shared;
using NUnit.Framework;
using Refit;
using RichardSzalay.MockHttp;

namespace GitTrends.UnitTests
{
    class RepositoryViewModelTests_MaximimApiCallLimit_RestApi : RepositoryViewModelTests_MaximimApiCallLimit
    {
        [Test]
        public Task PullToRefreshCommandTest_MaximumApiLimit_RestLApi() => ExecutePullToRefreshCommandTestMaximumApiLimitTest();

        protected override void InitializeServiceCollection()
        {
            var gitHubApiV3Client = RestService.For<IGitHubApiV3>(CreateMaximumApiLimitHttpClient(GitHubConstants.GitHubRestApiUrl));
            var gitHubGraphQLCLient = RestService.For<IGitHubGraphQLApi>(BaseApiService.CreateHttpClient(GitHubConstants.GitHubGraphQLApi));
            var azureFunctionsClient = RestService.For<IAzureFunctionsApi>(BaseApiService.CreateHttpClient(AzureConstants.AzureFunctionsApiUrl));

            ServiceCollection.Initialize(azureFunctionsClient, gitHubApiV3Client, gitHubGraphQLCLient);
        }

        protected static HttpClient CreateMaximumApiLimitHttpClient(string url)
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.Forbidden);
            responseMessage.Headers.Add(GitHubApiExceptionService.RateLimitRemainingHeader, "0");
            responseMessage.Headers.Add(GitHubApiExceptionService.RateLimitResetHeader, DateTimeOffset.UtcNow.AddMinutes(50).ToUnixTimeSeconds().ToString());

            var httpMessageHandler = new MockHttpMessageHandler();
            httpMessageHandler.When($"{url}/*").Respond(request => responseMessage);

            var httpClient = httpMessageHandler.ToHttpClient();
            httpClient.BaseAddress = new Uri(url);

            return httpClient;
        }
    }
}
