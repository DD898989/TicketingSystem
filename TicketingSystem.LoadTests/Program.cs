using NBomber.CSharp;
using System.Net.Http.Json;

namespace TicketingSystem.LoadTests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   Starting NBomber Load Test for /book endpoint");
        Console.WriteLine("=================================================");

        using var httpClient = new HttpClient();

        // 1. Define the Scenario using NBomber v4+ Functional Style
        var scenario = Scenario.Create("ticket_booking_load_test", async context =>
        {
            var memberId = $"M_{Guid.NewGuid()}";
            var payload = new 
            {
                ConcertId = "C101",
                AreaId = "A1",
                MemberId = memberId,
                Quantity = 1
            };

            // Run the step and measure latency
            await Step.Run("book_ticket", context, async () =>
            {
                try
                {
                    var response = await httpClient.PostAsJsonAsync("http://localhost:8080/book", payload);

                    if (response.IsSuccessStatusCode)
                    {
                        return Response.Ok(statusCode: "200");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var contentStr = await response.Content.ReadAsStringAsync();
                        if (int.TryParse(contentStr, out int code))
                        {
                            if (code == -2) // insufficientStockCode
                            {
                                return Response.Ok(statusCode: "Out_of_Stock");
                            }
                            else if (code == -1) // exceedLimitCode
                            {
                                return Response.Ok(statusCode: "Exceed_Limit");
                            }
                        }
                        return Response.Fail(statusCode: "400", message: $"BadRequest: {contentStr}");
                    }
                    else
                    {
                        return Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: response.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "Exception", message: ex.Message);
                }
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(
            // Maintain 200 concurrent users for 10 seconds
            Simulation.KeepConstant(copies: 200, during: TimeSpan.FromSeconds(10))
        );

        // 2. Run the benchmark scenario and output reports
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
