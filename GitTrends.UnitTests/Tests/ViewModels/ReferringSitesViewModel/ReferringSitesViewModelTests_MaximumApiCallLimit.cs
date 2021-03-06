﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GitTrends.Mobile.Common;
using GitTrends.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Refit;
using RichardSzalay.MockHttp;

namespace GitTrends.UnitTests
{
    class ReferringSitesViewModelTests_MaximumApiCallLimit : BaseTest
    {
        [Test]
        public async Task PullToRefreshTest_MaximumApiCallLimit()
        {
            //Arrange
            PullToRefreshFailedEventArgs pullToRefreshFailedEventArgs;
            string emptyDataViewTitle_Initial, emptyDataViewTitle_Final;
            string emptyDataViewDescription_Initial, emptyDataViewDescription_Final;
            bool isEmptyDataViewEnabled_Initial, isEmptyDataViewEnabled_DuringRefresh, isEmptyDataViewEnabled_Final;
            IReadOnlyList<MobileReferringSiteModel> mobileReferringSites_Initial, mobileReferringSites_DuringRefresh, mobileReferringSites_Final;

            var gitHubUserService = ServiceCollection.ServiceProvider.GetRequiredService<GitHubUserService>();
            var referringSitesViewModel = ServiceCollection.ServiceProvider.GetRequiredService<ReferringSitesViewModel>();
            var gitHubGraphQLApiService = ServiceCollection.ServiceProvider.GetRequiredService<GitHubGraphQLApiService>();

            var pullToRefreshFailedTCS = new TaskCompletionSource<PullToRefreshFailedEventArgs>();
            ReferringSitesViewModel.PullToRefreshFailed += HandlePullToRefreshFailed;

            //Act
            await AuthenticateUser(gitHubUserService, gitHubGraphQLApiService).ConfigureAwait(false);

            emptyDataViewTitle_Initial = referringSitesViewModel.EmptyDataViewTitle;
            isEmptyDataViewEnabled_Initial = referringSitesViewModel.IsEmptyDataViewEnabled;
            mobileReferringSites_Initial = referringSitesViewModel.MobileReferringSitesList;
            emptyDataViewDescription_Initial = referringSitesViewModel.EmptyDataViewDescription;

            var refreshCommandTask = referringSitesViewModel.RefreshCommand.ExecuteAsync((GitTrendsRepoOwner, GitTrendsRepoName, $"https://github.com/{GitTrendsRepoOwner}/{GitTrendsRepoName}", CancellationToken.None));

            isEmptyDataViewEnabled_DuringRefresh = referringSitesViewModel.IsEmptyDataViewEnabled;
            mobileReferringSites_DuringRefresh = referringSitesViewModel.MobileReferringSitesList;

            await refreshCommandTask.ConfigureAwait(false);

            emptyDataViewTitle_Final = referringSitesViewModel.EmptyDataViewTitle;
            isEmptyDataViewEnabled_Final = referringSitesViewModel.IsEmptyDataViewEnabled;
            mobileReferringSites_Final = referringSitesViewModel.MobileReferringSitesList;
            emptyDataViewDescription_Final = referringSitesViewModel.EmptyDataViewDescription;

            pullToRefreshFailedEventArgs = await pullToRefreshFailedTCS.Task.ConfigureAwait(false);

            //Asset
            Assert.IsFalse(isEmptyDataViewEnabled_Initial);
            Assert.IsFalse(isEmptyDataViewEnabled_DuringRefresh);
            Assert.True(isEmptyDataViewEnabled_Final);

            Assert.IsEmpty(mobileReferringSites_Initial);
            Assert.IsEmpty(mobileReferringSites_DuringRefresh);
            Assert.IsEmpty(mobileReferringSites_Final);

            Assert.AreEqual(EmptyDataViewService.GetReferringSitesTitleText(RefreshState.MaximumApiLimit), emptyDataViewTitle_Final);
            Assert.AreEqual(EmptyDataViewService.GetReferringSitesTitleText(RefreshState.Uninitialized), emptyDataViewTitle_Initial);

            Assert.AreEqual(EmptyDataViewService.GetReferringSitesDescriptionText(RefreshState.MaximumApiLimit), emptyDataViewDescription_Final);
            Assert.AreEqual(EmptyDataViewService.GetReferringSitesDescriptionText(RefreshState.Uninitialized), emptyDataViewDescription_Initial);

            Assert.IsTrue(pullToRefreshFailedEventArgs is MaximimApiRequestsReachedEventArgs);

            void HandlePullToRefreshFailed(object? sender, PullToRefreshFailedEventArgs e)
            {
                ReferringSitesViewModel.PullToRefreshFailed -= HandlePullToRefreshFailed;
                pullToRefreshFailedTCS.SetResult(e);
            }
        }

        protected override void InitializeServiceCollection()
        {
            var gitHubApiV3Client = RestService.For<IGitHubApiV3>(CreateMaximumApiLimitHttpClient(GitHubConstants.GitHubRestApiUrl));
            var gitHubGraphQLCLient = RestService.For<IGitHubGraphQLApi>(BaseApiService.CreateHttpClient(GitHubConstants.GitHubGraphQLApi));
            var azureFunctionsClient = RestService.For<IAzureFunctionsApi>(BaseApiService.CreateHttpClient(AzureConstants.AzureFunctionsApiUrl));

            ServiceCollection.Initialize(azureFunctionsClient, gitHubApiV3Client, gitHubGraphQLCLient);
        }

        static HttpClient CreateMaximumApiLimitHttpClient(string url)
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
