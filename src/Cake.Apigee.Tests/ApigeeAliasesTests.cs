using System;
using System.Linq;
using System.Net;
using System.Net.Http;

using Xunit;
using Xunit.Abstractions;

namespace Cake.Apigee.Tests
{
    public class ApigeeAliasesTests
    {
        private readonly ITestOutputHelper output;

        public ApigeeAliasesTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void GivenImportProxy_WhenSettingsNotSupplied_ThenImportProxy()
        {
            // Arrange
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseSuccessfulImportResponse();    

            // Act
            ApigeeAliases.ImportProxy(fixture.ContextMock.Object, "org", "proxy", fixture.GetProxyZipFilePath());

            // Assert
            Assert.Equal(new Uri("https://api.enterprise.apigee.com/v1/organizations/org/apis?action=import&name=proxy"), fixture.RequestUrl);
        }

        [Fact]
        public void GivenImportProxy_WhenImportFails_ThenUsefulErrorShown()
        {
            // Arrange
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseFailedImportResponse();

            // Act
            try
            {
                ApigeeAliases.ImportProxy(fixture.ContextMock.Object, "org", "proxy", fixture.GetProxyZipFilePath());
            }
            catch (Exception ex)
            {
                Assert.True(ex.Message.StartsWith("Apigee returned BadRequest"));
            }
        }

        [Fact]
        public void GivenImportProxy_WhenCredentialsSupplied_ThenCredentialsAddedToAuthorizationHeader()
        {
            // Arrange     
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseSuccessfulImportResponse();

            // Act
            var credentials = new Credentials { Username = "testUser", Password = "testPassword" };
            ApigeeAliases.ImportProxy(fixture.ContextMock.Object, "org", "proxy", fixture.GetProxyZipFilePath(), new ImportProxySettings { Credentials = credentials });

            // Assert
            Assert.Equal("Basic", fixture.FakeResponseHandler.Requests.First().Headers.Authorization.Scheme);
        }

        [Fact]
        public void GivenInstallNodePackagedModules_WhenModulesRestored_ThenModulesAndVersionsReturned()
        {
            // Arrange     
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseSuccessfulNpmInstallResponse();

            // Act
            var modules = ApigeeAliases.InstallNodePackagedModules(fixture.ContextMock.Object, "org", "proxy", "123");

            // Assert
            Assert.Equal(14, modules.Length);
            Assert.Equal("express-xml-bodyparser", modules[0].Name);
            Assert.Equal("0.3.0", modules[0].Version);
        }

        [Fact]
        public void GivenGetApiProxy_WhenProxyReturned_ThenRevisionsAvailable()
        {
            // Arrange     
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseSuccessfulGetApiProxyResponse();

            // Act            
            var proxy = ApigeeAliases.GetApiProxy(fixture.ContextMock.Object, "org", "proxy");

            // Assert
            Assert.Equal(9, proxy.Revision.Count());
        }

        [Fact]
        public void GivenDeleteApiProxyRevision_WhenFound_ThenSuccess()
        {
            // Arrange     
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;

            fixture.UseSuccessfulGetApiProxyResponse();

            // Act            
            ApigeeAliases.DeleteApiProxyRevision(fixture.ContextMock.Object, "org", "proxy", "123");
        }

        [Fact]
        public void GivenDeleteApiProxyRevision_WhenNotFound_ThenError()
        {
            // Arrange     
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;
            fixture.Use(HttpStatusCode.NotFound, "DeleteApiProxyRevisionWhenNotFound.json");

            // Act            
            Assert.Throws<Exception>(() => ApigeeAliases.DeleteApiProxyRevision(fixture.ContextMock.Object, "org", "proxy", "123"));
        }

        [Fact]
        public void GivenDeleteAllUndeployedApiProxyRevisions_WhenAllRevisionsDeleted_ThenSucceed()
        {
            // Arrange     
            Uri baseUri = new Uri("https://api.enterprise.apigee.com");
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;
            fixture.FakeResponseHandler.AddFakeResponse(new Uri(baseUri, $"v1/organizations/org/apis/proxy"), new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResourceHelper.GetResourceAsString("GetApiProxyResponse.json")) });
            for (var revision = 1; revision < 10; revision++)
            {
                fixture.FakeResponseHandler.AddFakeResponse(new Uri(baseUri, $"v1/organizations/org/apis/proxy/revisions/" + revision), new HttpResponseMessage(HttpStatusCode.OK));
            }

            // Act            
            ApigeeAliases.DeleteAllUndeployedApiProxyRevisions(fixture.ContextMock.Object, "org", "proxy");
        }

        [Fact]
        public void GivenDeleteAllUndeployedApiProxyRevisions_WhenARevisionIsDeployed_ThenDeleteAllOtherRevisionsAndSucceed()
        {
            // Arrange     
            Uri baseUri = new Uri("https://api.enterprise.apigee.com");
            var fixture = new ApigeeAliasesFixture(this.output);
            ApigeeAliases.ApigeeProxyManagementService = fixture.ApigeeProxyManagementService;
            fixture.FakeResponseHandler.AddFakeResponse(new Uri(baseUri, $"v1/organizations/org/apis/proxy"), new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResourceHelper.GetResourceAsString("GetApiProxyResponse.json")) });
            for (var revision = 1; revision < 10; revision++)
            {
                var deployedRevisionNumber = 5;
                if (revision == deployedRevisionNumber)
                {
                    fixture.FakeResponseHandler.AddFakeResponse(
                        new Uri(baseUri, $"v1/organizations/org/apis/proxy/revisions/" + revision),
                        new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ResourceHelper.GetResourceAsString("DeleteApiProxyRevisionWhenDeployedResponse.json")) });
                }
                else
                {
                    fixture.FakeResponseHandler.AddFakeResponse(
                        new Uri(baseUri, $"v1/organizations/org/apis/proxy/revisions/" + revision),
                        new HttpResponseMessage(HttpStatusCode.OK));
                }
            }

            // Act            
            ApigeeAliases.DeleteAllUndeployedApiProxyRevisions(fixture.ContextMock.Object, "org", "proxy");
        }
    }
}
