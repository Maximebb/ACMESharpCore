using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACMESharp.Authorizations;
using ACMESharp.Crypto;
using ACMESharp.Testing.Xunit;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ACMESharp.IntegrationTests
{
    [Collection(nameof(AcmeOrderTests))]
    [CollectionDefinition(nameof(AcmeOrderTests))]
    [TestOrder(0_10)]
    public class AcmeSingleNameOrderTests : AcmeOrderTests
    {
        public AcmeSingleNameOrderTests(ITestOutputHelper output,
                StateFixture state, ClientsFixture clients, AwsFixture aws)
            : base(output, state, clients, aws,
                    state.Factory.CreateLogger(typeof(AcmeSingleNameOrderTests).FullName))
        { }

        [Fact]
        [TestOrder(0_210, "SingleHttp")]
        public async Task Test_Create_Order_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var dnsNames = new[] {
                TestHttpSubdomain,
            };
            tctx.GroupSaveObject("order_names.json", dnsNames);
            Log.LogInformation("Using Test DNS subdomain name: {0}", dnsNames);

            var order = await Clients.Acme.CreateOrderAsync(dnsNames);
            tctx.GroupSaveObject("order.json", order);
        }

        [Fact]
        [TestOrder(0_215, "SingleHttp")]
        public async Task Test_Create_OrderDuplicate_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldNames = tctx.GroupLoadObject<string[]>("order_names.json");
            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            Assert.NotNull(oldNames);
            Assert.Equal(1, oldNames.Length);
            Assert.NotNull(oldOrder);
            Assert.NotNull(oldOrder.OrderUrl);

            var newOrder = await Clients.Acme.CreateOrderAsync(oldNames);
            tctx.GroupSaveObject("order-dup.json", newOrder);

            ValidateDuplicateOrder(oldOrder, newOrder);
        }

        [Fact]
        [TestOrder(0_220, "SingleHttp")]
        public void Test_Decode_OrderChallengeForHttp01_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    Log.LogInformation("Decoding Authorization {0} Challenge {1}",
                            authzIndex, chlngIndex);
                    
                    var chlngDetails = AuthorizationDecoder.ResolveChallengeForHttp01(
                            authz, chlng, Clients.Acme.Signer);

                    Assert.Equal(Http01ChallengeValidationDetails.Http01ChallengeType,
                            chlngDetails.ChallengeType, ignoreCase: true);
                    Assert.NotNull(chlngDetails.HttpResourceUrl);
                    Assert.NotNull(chlngDetails.HttpResourcePath);
                    Assert.NotNull(chlngDetails.HttpResourceContentType);
                    Assert.NotNull(chlngDetails.HttpResourceValue);

                    tctx.GroupSaveObject($"order-authz_{authzIndex}-chlng_{chlngIndex}.json",
                            chlngDetails);

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }

        [Fact]
        [TestOrder(0_230, "SingleHttp")]
        public async Task Test_Create_OrderAnswerHttpContent_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    var chlngDetails = tctx.GroupLoadObject<Http01ChallengeValidationDetails>(
                            $"order-authz_{authzIndex}-chlng_{chlngIndex}.json");

                    Log.LogInformation("Creating HTTP Content for Authorization {0} Challenge {1} as per {@Details}",
                            authzIndex, chlngIndex, chlngDetails);

                    await Aws.S3.EditFile(
                            chlngDetails.HttpResourcePath,
                            chlngDetails.HttpResourceContentType,
                            chlngDetails.HttpResourceValue);

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }

        [Fact]
        [TestOrder(0_235, "SingleHttp")]
        public async Task Test_Exists_OrderAnswerHttpContent_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            Thread.Sleep(1*1000);

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    var chlngDetails = tctx.GroupLoadObject<Http01ChallengeValidationDetails>(
                            $"order-authz_{authzIndex}-chlng_{chlngIndex}.json");

                    Log.LogInformation("Waiting on HTTP content for Authorization {0} Challenge {1} as per {@Details}",
                            authzIndex, chlngIndex, chlngDetails);
 
                    var created = await ValidateHttpContent(chlngDetails.HttpResourceUrl,
                            contentType: chlngDetails.HttpResourceContentType,
                            targetValue: chlngDetails.HttpResourceValue);

                    Assert.True(created, "    Failed HTTP set/read expected content");

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }

        [Fact]
        [TestOrder(0_240, "SingleHttp")]
        public async Task Test_Answer_OrderChallenges_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    Log.LogInformation("Answering Authorization {0} Challenge {1}", authzIndex, chlngIndex);
                    var updated = await Clients.Acme.AnswerChallengeAsync(authz, chlng);

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }

        [Fact]
        [TestOrder(0_245, "SingleHttp")]
        public async Task Test_AreValid_OrderChallengesAndAuthorization_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    int maxTry = 20;
                    int trySleep = 5 * 1000;
                    
                    for (var tryCount = 0; tryCount < maxTry; ++tryCount)
                    {
                        if (tryCount > 0)
                            // Wait just a bit for
                            // subsequent queries
                            Thread.Sleep(trySleep);

                        var updatedChlng = await Clients.Acme.RefreshChallengeAsync(authz, chlng);

                        // The Challenge is either Valid, still Pending or some other UNEXPECTED state

                        if ("valid" == updatedChlng.Status)
                        {
                            Log.LogInformation("    Authorization {0} Challenge {1} is VALID!", authzIndex, chlngIndex);
                            break;
                        }

                        if ("pending" != updatedChlng.Status)
                        {
                            Log.LogInformation("    Authorization {0} Challenge {1} in **UNEXPECTED STATUS**: {@UpdateChallengeDetails}",
                                    authzIndex, chlngIndex, updatedChlng);
                            throw new InvalidOperationException("Unexpected status for answered Challenge: " + updatedChlng.Status);
                        }
                    }

                    ++chlngIndex;
                }
                ++authzIndex;

                var updatedAuthz = await Clients.Acme.RefreshAuthorizationAsync(authz);
                Assert.Equal("valid", updatedAuthz.Details.Status);
            }
        }

        // [Fact]
        // [TestOrder(0_250, "SingleHttp")]
        // public async Task Test_IsValid_OrderStatus_ForSingleHttp()
        // {
        //     var tctx = SetTestContext();

        //     // TODO: Validate overall order status is "valid"

        //     // This state is expected based on the ACME spec
        //     // BUT -- LE's implementation does not appear to
        //     // respect this contract -- the status of the
        //     // Order stays in the pending state even though
        //     // we are able to successfully Finalize the Order
        // }

        [Fact]
        [TestOrder(0_260, "SingleHttp")]
        public async Task Test_Finalize_Order_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var rsaKeys = CryptoHelper.GenerateRsaKeys(4096);
            var rsa = CryptoHelper.GenerateRsaAlgorithm(rsaKeys);
            tctx.GroupWriteTo("order-csr-keys.txt", rsaKeys);
            var derEncodedCsr = CryptoHelper.GenerateCsr(oldOrder.DnsIdentifiers, rsa);
            tctx.GroupWriteTo("order-csr.der", derEncodedCsr);

            var updatedOrder = await Clients.Acme.FinalizeOrderAsync(oldOrder, derEncodedCsr);

            int maxTry = 20;
            int trySleep = 5 * 1000;
            var valid = false;

            for (var tryCount = 0; tryCount < maxTry; ++tryCount)
            {
                if (tryCount > 0)
                {
                    // Wait just a bit for
                    // subsequent queries
                    Thread.Sleep(trySleep);

                    // Only need to refresh
                    // after the first check
                    Log.LogInformation($"  Retry #{tryCount} refreshing Order");
                    updatedOrder = await Clients.Acme.RefreshOrderAsync(oldOrder);
                    tctx.GroupSaveObject("order-updated.json", updatedOrder);
                }

                if (!valid)
                {
                    // The Order is either Valid, still Pending or some other UNEXPECTED state

                    if ("valid" == updatedOrder.Status)
                    {
                        valid = true;
                        Log.LogInformation("Order is VALID!");
                    }
                    else if ("pending" != updatedOrder.Status)
                    {
                        Log.LogInformation("Order in **UNEXPECTED STATUS**: {@UpdateChallengeDetails}", updatedOrder);
                        throw new InvalidOperationException("Unexpected status for Order: " + updatedOrder.Status);
                    }
                }

                if (valid)
                {
                    // Once it's valid, then we need to wait for the Cert
                    
                    if (!string.IsNullOrEmpty(updatedOrder.CertificateUrl))
                    {
                        Log.LogInformation("Certificate URL is ready!");
                        break;
                    }
                }
            }

            Assert.NotNull(updatedOrder.CertificateUrl);

            var certBytes = await Clients.Http.GetByteArrayAsync(updatedOrder.CertificateUrl);
            tctx.GroupWriteTo("order-cert.crt", certBytes);
        }

        [Fact]
        [TestOrder(0_270, "SingleHttp")]
        public async Task Test_Delete_OrderAnswerHttpContent_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    var chlngDetails = tctx.GroupLoadObject<Http01ChallengeValidationDetails>(
                            $"order-authz_{authzIndex}-chlng_{chlngIndex}.json");

                    Log.LogInformation("Deleting HTTP content for Authorization {0} Challenge {1} as per {@Details}",
                            authzIndex, chlngIndex, chlngDetails);

                    await Aws.S3.EditFile(
                            chlngDetails.HttpResourcePath,
                            null, null);

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }
        
        [Fact]
        [TestOrder(0_275, "SingleHttp")]
        public async Task Test_IsDeleted_OrderAnswerHttpContent_ForSingleHttp()
        {
            var tctx = SetTestContext();

            var oldOrder = tctx.GroupLoadObject<AcmeOrder>("order.json");

            Thread.Sleep(1*1000);

            var authzIndex = 0;
            foreach (var authz in oldOrder.Authorizations)
            {
                var chlngIndex = 0;
                foreach (var chlng in authz.Details.Challenges.Where(
                    x => x.Type == Http01ChallengeValidationDetails.Http01ChallengeType))
                {
                    var chlngDetails = tctx.GroupLoadObject<Http01ChallengeValidationDetails>(
                            $"order-authz_{authzIndex}-chlng_{chlngIndex}.json");

                    Log.LogInformation("Waiting on HTTP content deleted for Authorization {0} Challenge {1} as per {@Details}",
                            authzIndex, chlngIndex, chlngDetails);
 
                    var deleted = await ValidateHttpContent(chlngDetails.HttpResourceUrl,
                            targetMissing: true);

                    Assert.True(deleted, "    Failed HTTP content delete/read expected missing TXT record");

                    ++chlngIndex;
                }
                ++authzIndex;
            }
        }
    }
}