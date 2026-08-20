using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TicketingSystem.BddTests.Support;
using TicketingSystem.Endpoints;

namespace TicketingSystem.BddTests.StepDefinitions;

[Binding]
public class TicketingStepDefinitions
{
    private readonly TestHost _host;
    private string _concertId = string.Empty;
    private string _areaId = string.Empty;
    private HttpResponseMessage? _lastResponse;

    // Reqnroll automatically resolves and injects the same instance of TestHost 
    // into this class constructor for the duration of a scenario.
    public TicketingStepDefinitions(TestHost host)
    {
        _host = host;
    }

    [Given("the ticketing system is freshly initialized and reset")]
    public async Task GivenTheTicketingSystemIsFreshlyInitializedAndReset()
    {
        var response = await _host.Client.PostAsJsonAsync("/init", Array.Empty<Init.InitRequest>());
        response.EnsureSuccessStatusCode();
    }

    [Given("a concert with ID \"(.*)\" and area with ID \"(.*)\"")]
    public void GivenAConcertWithIDAndAreaWithID(string concertId, string areaId)
    {
        _concertId = concertId;
        _areaId = areaId;
    }

    [When("I initialize the ticket stock with (\\d+) tickets")]
    public async Task WhenIInitializeTheTicketStockWithTickets(int totalTickets)
    {
        var request = new Init.InitRequest(_concertId, _areaId, totalTickets);
        _lastResponse = await _host.Client.PostAsJsonAsync("/init", new[] { request });
    }

    [Then("the system should respond with success")]
    public void ThenTheSystemShouldRespondWithSuccess()
    {
        _lastResponse.Should().NotBeNull();
        _lastResponse!.IsSuccessStatusCode.Should().BeTrue();
    }

    [Then("the stock for concert \"(.*)\" and area \"(.*)\" should be (\\d+)")]
    [Then("the stock for concert \"(.*)\" and area \"(.*)\" should remain (\\d+)")]
    public async Task ThenTheStockForConcertAndAreaShouldBe(string concertId, string areaId, int expectedStock)
    {
        var key = $"ticketLeft:{concertId}:{areaId}";
        var leftStr = await _host.GetRedisDatabase().StringGetAsync(key);
        leftStr.HasValue.Should().BeTrue($"Redis key '{key}' should exist");
        int.Parse(leftStr.ToString()).Should().Be(expectedStock);
    }

    [Given("a concert with ID \"(.*)\" and area with ID \"(.*)\" initialized with (\\d+) tickets")]
    public async Task GivenAConcertWithIDAndAreaWithIDInitializedWithTickets(string concertId, string areaId, int totalTickets)
    {
        _concertId = concertId;
        _areaId = areaId;
        var request = new Init.InitRequest(_concertId, _areaId, totalTickets);
        var response = await _host.Client.PostAsJsonAsync("/init", new[] { request });
        response.EnsureSuccessStatusCode();
    }

    [When("Member \"(.*)\" books (\\d+) tickets")]
    [When("Member \"(.*)\" books (\\d+) more tickets")]
    public async Task WhenMemberBooksTickets(string memberId, int quantity)
    {
        var req = new Book.BookRequest(_concertId, _areaId, memberId, quantity);
        _lastResponse = await _host.Client.PostAsJsonAsync("/book", req);
    }

    [Then("the booking should succeed")]
    public void ThenTheBookingShouldSucceed()
    {
        _lastResponse.Should().NotBeNull();
        _lastResponse!.IsSuccessStatusCode.Should().BeTrue();
    }

    [Then("the booking should fail with an error code for exceeding the limit of (-\\d+)")]
    public async Task ThenTheBookingShouldFailWithAnErrorCodeForExceedingTheLimitOf(int expectedErrorCode)
    {
        _lastResponse.Should().NotBeNull();
        _lastResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var bodyStr = await _lastResponse!.Content.ReadAsStringAsync();
        int.Parse(bodyStr).Should().Be(expectedErrorCode);
    }

    [Then("the booking should fail with an error code for insufficient stock of (-\\d+)")]
    public async Task ThenTheBookingShouldFailWithAnErrorCodeForInsufficientStockOf(int expectedErrorCode)
    {
        _lastResponse.Should().NotBeNull();
        _lastResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var bodyStr = await _lastResponse!.Content.ReadAsStringAsync();
        int.Parse(bodyStr).Should().Be(expectedErrorCode);
    }

    [Given("Member \"(.*)\" has booked (\\d+) tickets")]
    public async Task GivenMemberHasBookedTickets(string memberId, int quantity)
    {
        var req = new Book.BookRequest(_concertId, _areaId, memberId, quantity);
        var response = await _host.Client.PostAsJsonAsync("/book", req);
        response.EnsureSuccessStatusCode();
    }

    [Then("eventually an order should be saved in the database for concert \"(.*)\", area \"(.*)\", member \"(.*)\", with quantity (\\d+)")]
    [Given("eventually an order should be saved in the database for concert \"(.*)\", area \"(.*)\", member \"(.*)\", with quantity (\\d+)")]
    public async Task ThenEventuallyAnOrderShouldBeSavedInTheDatabaseForConcertAreaMemberWithQuantity(
        string concertId, string areaId, string memberId, int expectedQuantity)
    {
        bool found = false;
        int actualQuantity = 0;
        var startTime = DateTime.UtcNow;
        // Wait up to 5 seconds for the background consumer to process and write the SQL DB entry
        while ((DateTime.UtcNow - startTime).TotalSeconds < 5)
        {
            using var db = _host.GetDbContext();
            var order = await db.Orders.FirstOrDefaultAsync(o => 
                o.ConcertId == concertId && 
                o.AreaId == areaId && 
                o.MemberId == memberId);
            if (order != null)
            {
                found = true;
                actualQuantity = order.Quantity;
                break;
            }
            await Task.Delay(100);
        }

        found.Should().BeTrue("Expected order to be eventually saved to the SQL database by the background consumer service");
        actualQuantity.Should().Be(expectedQuantity);
    }

    [When("I reset the system")]
    public async Task WhenIResetTheSystem()
    {
        var response = await _host.Client.PostAsJsonAsync("/init", Array.Empty<Init.InitRequest>());
        response.EnsureSuccessStatusCode();
    }

    [Then("the database should contain (\\d+) orders")]
    public async Task ThenTheDatabaseShouldContainOrders(int expectedCount)
    {
        using var db = _host.GetDbContext();
        var count = await db.Orders.CountAsync();
        count.Should().Be(expectedCount);
    }

    [Then("there should be no keys left in Redis")]
    public void ThenThereShouldBeNoKeysLeftInRedis()
    {
        var server = _host.GetRedisDatabase().Multiplexer.GetServer(_host.GetRedisDatabase().Multiplexer.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "*").ToList();
        keys.Should().BeEmpty();
    }
}
