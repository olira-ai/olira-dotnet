using System.Text.Json;
using Olira.Internal;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

public class LogQueryBuilderTests
{
    private static (MockHttpMessageHandler Mock, HttpTransport Transport, List<JsonElement> Bodies) Setup(
        string path,
        string responseJson)
    {
        var bodies = new List<JsonElement>();
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}{path}",
            responseJson,
            body => bodies.Add(body));
        return (mock, TestHelpers.CreateTransport(mock), bodies);
    }

    [Fact]
    public void Eq_AppendsFilterNode()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Eq("type", "health_metric_reported").Execute();
        }

        var filter = bodies[0].RequireProperty("filter");
        Assert.Equal(JsonValueKind.Array, filter.ValueKind);
        Assert.Equal(1, filter.GetArrayLength());
        Assert.Equal("type", filter[0].GetProperty("field").GetString());
        Assert.Equal("eq", filter[0].GetProperty("op").GetString());
        Assert.Equal("health_metric_reported", filter[0].GetProperty("value").GetString());
    }

    [Fact]
    public void MultipleFilters_Chain()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Gt("payload.score", 5).Lt("payload.score", 10).Execute();
        }

        var filter = bodies[0].RequireProperty("filter");
        Assert.Equal(2, filter.GetArrayLength());
        Assert.Equal("gt", filter[0].GetProperty("op").GetString());
        Assert.Equal(5, filter[0].GetProperty("value").GetInt32());
        Assert.Equal("lt", filter[1].GetProperty("op").GetString());
        Assert.Equal(10, filter[1].GetProperty("value").GetInt32());
    }

    [Fact]
    public void In_CoercesToList()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").In("type", ["a", "b", "c"]).Execute();
        }

        var value = bodies[0].RequireProperty("filter")[0].GetProperty("value");
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(["a", "b", "c"], value.EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [Fact]
    public void Nin_BuildsFilter()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Nin("type", ["x"]).Execute();
        }

        var node = bodies[0].RequireProperty("filter")[0];
        Assert.Equal("nin", node.GetProperty("op").GetString());
        Assert.Equal("x", node.GetProperty("value")[0].GetString());
    }

    [Theory]
    [InlineData("neq")]
    [InlineData("gte")]
    [InlineData("lte")]
    [InlineData("like")]
    [InlineData("ilike")]
    [InlineData("is")]
    [InlineData("exists")]
    [InlineData("contains")]
    public void AllScalarOperators(string op)
    {
        var (_, transport, bodies) = Setup("/v1/state/p/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            var q = new LogQuery(transport, patientId: "p");
            switch (op)
            {
                case "neq": q.Neq("f", 1); break;
                case "gte": q.Gte("f", 1); break;
                case "lte": q.Lte("f", 1); break;
                case "like": q.Like("f", "%x%"); break;
                case "ilike": q.ILike("f", "%x%"); break;
                case "is": q.Is("f", null); break;
                case "exists": q.Exists("f", true); break;
                case "contains": q.Contains("f", "val"); break;
            }

            q.Execute();
        }

        Assert.Equal(op, bodies[0].RequireProperty("filter")[0].GetProperty("op").GetString());
    }

    [Fact]
    public void UnknownOperator_RaisesValidationError()
    {
        using var transport = TestHelpers.CreateTransport(new MockHttpMessageHandler());
        var ex = Assert.Throws<ValidationError>(() =>
            new LogQuery(transport, patientId: "p1").Filter("type", "regex", ".*").Execute());
        Assert.Contains("unknown operator", ex.Message);
    }

    [Fact]
    public void Or_WithFExpressions()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1")
                .Or(new F("payload.score").Gt(6), new F("type").Eq("mood"))
                .Execute();
        }

        var or = bodies[0].RequireProperty("filter")[0].GetProperty("or");
        Assert.Equal(2, or.GetArrayLength());
        Assert.Equal("gt", or[0].GetProperty("op").GetString());
        Assert.Equal("eq", or[1].GetProperty("op").GetString());
    }

    [Fact]
    public void And_Group()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1")
                .And(new F("type").Eq("a"), new F("type").Eq("b"))
                .Execute();
        }

        Assert.True(bodies[0].RequireProperty("filter")[0].TryGetProperty("and", out var and));
        Assert.Equal(2, and.GetArrayLength());
    }

    [Fact]
    public void F_AllOperators()
    {
        var f = new F("payload.x");
        Assert.Equal("neq", f.Neq(1)["op"]);
        Assert.Equal("gte", f.Gte(1)["op"]);
        Assert.Equal("lte", f.Lte(1)["op"]);
        Assert.Equal("in", f.In(["a"])["op"]);
        Assert.Equal("nin", f.Nin(["a"])["op"]);
        Assert.Equal("like", f.Like("%x")["op"]);
        Assert.Equal("ilike", f.ILike("%x")["op"]);
        Assert.Equal("is", f.Is(null)["op"]);
        Assert.Equal("exists", f.Exists()["op"]);
        Assert.Equal("contains", f.Contains("v")["op"]);
    }

    [Fact]
    public void Select_Positional()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Select("timestamp", "type").Execute();
        }

        var sel = bodies[0].RequireProperty("select");
        Assert.Equal(2, sel.GetArrayLength());
        Assert.Equal("timestamp", sel[0].GetProperty("path").GetString());
        Assert.Equal("type", sel[1].GetProperty("path").GetString());
    }

    [Fact]
    public void SelectAliases()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").SelectAliases(("severity", "payload.score")).Execute();
        }

        var node = bodies[0].RequireProperty("select")[0];
        Assert.Equal("payload.score", node.GetProperty("path").GetString());
        Assert.Equal("severity", node.GetProperty("alias").GetString());
    }

    [Fact]
    public void Select_DictionaryAliases_PythonKwargsShape()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1")
                .Select(new Dictionary<string, string> { ["severity"] = "payload.score" })
                .Execute();
        }

        var node = bodies[0].RequireProperty("select")[0];
        Assert.Equal("payload.score", node.GetProperty("path").GetString());
        Assert.Equal("severity", node.GetProperty("alias").GetString());
    }

    [Fact]
    public void SelectArray_Shape()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1")
                .SelectArray(
                    "payload.items",
                    where: new F("payload.items").Gt(0),
                    element: "name",
                    first: true,
                    alias: "first_item")
                .Execute();
        }

        var node = bodies[0].RequireProperty("select")[0];
        Assert.Equal("payload.items", node.GetProperty("path").GetString());
        Assert.True(node.GetProperty("first").GetBoolean());
        Assert.Equal("first_item", node.GetProperty("alias").GetString());
        Assert.Equal("name", node.GetProperty("element").GetString());
        Assert.True(node.TryGetProperty("where", out _));
    }

    [Fact]
    public void Order_Limit_Offset_Range()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Order("timestamp", desc: true).Limit(10).Offset(20).Execute();
        }

        Assert.True(bodies[0].RequireProperty("order")[0].GetProperty("desc").GetBoolean());
        Assert.Equal(10, bodies[0].GetProperty("limit").GetInt32());
        Assert.Equal(20, bodies[0].GetProperty("offset").GetInt32());

        bodies.Clear();
        var mock2 = new MockHttpMessageHandler();
        mock2.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/state/p1/logs/query",
            TestHelpers.EmptyQueryResultJson(),
            body => bodies.Add(body));
        using var transport2 = TestHelpers.CreateTransport(mock2);
        new LogQuery(transport2, patientId: "p1").Range(10, 19).Execute();
        Assert.Equal(10, bodies[0].GetProperty("offset").GetInt32());
        Assert.Equal(10, bodies[0].GetProperty("limit").GetInt32());
    }

    [Fact]
    public void GroupBy_AndAggregations()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").GroupBy("type").Avg("payload.score", "avg_score").Execute();
        }

        Assert.Equal("type", bodies[0].RequireProperty("group_by")[0].GetString());
        var agg = bodies[0].RequireProperty("aggregations")[0];
        Assert.Equal("avg", agg.GetProperty("op").GetString());
        Assert.Equal("payload.score", agg.GetProperty("field").GetString());
        Assert.Equal("avg_score", agg.GetProperty("alias").GetString());
    }

    [Fact]
    public void CountAgg()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").GroupBy("type").CountAgg("n").Execute();
        }

        var agg = bodies[0].RequireProperty("aggregations")[0];
        Assert.Equal("count", agg.GetProperty("op").GetString());
        Assert.Equal("n", agg.GetProperty("alias").GetString());
    }

    [Fact]
    public void SinglePatient_PostsToCorrectEndpoint()
    {
        var mock = new MockHttpMessageHandler();
        var hit = false;
        mock.When(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/state/abc/logs/query")
            .Respond(_ =>
            {
                hit = true;
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(TestHelpers.EmptyQueryResultJson()),
                };
            });
        using var transport = TestHelpers.CreateTransport(mock);
        new LogQuery(transport, patientId: "abc").Execute();
        Assert.True(hit);
    }

    [Fact]
    public void Population_PostsToOrgEndpoint_WithPatientIds()
    {
        var (_, transport, bodies) = Setup("/v1/state/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, patientIds: ["p1", "p2"], population: true).Execute();
        }

        var ids = bodies[0].RequireProperty("patient_ids").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(["p1", "p2"], ids);
    }

    [Fact]
    public void Population_WithoutIds_OmitsPatientIds()
    {
        var (_, transport, bodies) = Setup("/v1/state/logs/query", TestHelpers.EmptyQueryResultJson());
        using (transport)
        {
            new LogQuery(transport, population: true).Execute();
        }

        Assert.False(bodies[0].TryGetProperty("patient_ids", out _));
    }

    [Fact]
    public void Execute_ReturnsLogQueryResult()
    {
        var (_, transport, _) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":1,"rows":[{"id":"1","type":"t"}]}""");
        using (transport)
        {
            var result = new LogQuery(transport, patientId: "p1").Execute();
            Assert.Equal(1, result.Count);
            Assert.Equal("1", result[0]["id"]?.ToString());
        }
    }

    [Fact]
    public void Count_Terminal_SetsCountTrue()
    {
        var (_, transport, bodies) = Setup("/v1/state/p1/logs/query", """{"count":42,"rows":[]}""");
        using (transport)
        {
            var n = new LogQuery(transport, patientId: "p1").Count();
            Assert.Equal(42, n);
            Assert.True(bodies[0].GetProperty("count").GetBoolean());
        }
    }

    [Fact]
    public void Single_RaisesWhenZeroOrTwoRows()
    {
        var (_, transport, _) = Setup("/v1/state/p1/logs/query", """{"count":0,"rows":[]}""");
        using (transport)
        {
            var ex = Assert.Throws<ValidationError>(() => new LogQuery(transport, patientId: "p1").Single());
            Assert.Contains("expected exactly one row", ex.Message);
        }

        var (_, transport2, _) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":2,"rows":[{"id":"1"},{"id":"2"}]}""");
        using (transport2)
        {
            Assert.Throws<ValidationError>(() => new LogQuery(transport2, patientId: "p1").Single());
        }
    }

    [Fact]
    public void Single_ReturnsDict_AndDefaultsLimit2()
    {
        var (_, transport, bodies) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":1,"rows":[{"id":"42"}]}""");
        using (transport)
        {
            var row = new LogQuery(transport, patientId: "p1").Single();
            Assert.Equal("42", row["id"]?.ToString());
            Assert.Equal(2, bodies[0].GetProperty("limit").GetInt32());
        }
    }

    [Fact]
    public void Single_RespectsCallerLimit()
    {
        var (_, transport, bodies) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":1,"rows":[{"id":"1"}]}""");
        using (transport)
        {
            new LogQuery(transport, patientId: "p1").Limit(1).Single();
            Assert.Equal(1, bodies[0].GetProperty("limit").GetInt32());
        }
    }

    [Fact]
    public void MaybeSingle_Behavior()
    {
        var (_, transport, _) = Setup("/v1/state/p1/logs/query", """{"count":0,"rows":[]}""");
        using (transport)
        {
            Assert.Null(new LogQuery(transport, patientId: "p1").MaybeSingle());
        }

        var (_, transport2, _) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":2,"rows":[{"id":"1"},{"id":"2"}]}""");
        using (transport2)
        {
            var ex = Assert.Throws<ValidationError>(() => new LogQuery(transport2, patientId: "p1").MaybeSingle());
            Assert.Contains("expected at most one row", ex.Message);
        }
    }

    [Fact]
    public void Result_IsIterableAndIndexable()
    {
        var result = new LogQueryResult
        {
            Count = 2,
            Rows =
            [
                new Dictionary<string, object?> { ["a"] = 1 },
                new Dictionary<string, object?> { ["a"] = 2 },
            ],
        };
        Assert.Equal(2, result.Count());
        Assert.Equal(1, result[0]["a"]);
    }

    [Fact]
    public void AsLogs_ParsesIntoLogEntries()
    {
        var result = new LogQueryResult
        {
            Count = 1,
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["id"] = "x",
                    ["type"] = "health_metric_reported",
                    ["timestamp"] = "2026-01-01T00:00:00Z",
                    ["ingested_at"] = "2026-01-01T00:00:02Z",
                    ["payload"] = new Dictionary<string, object?>(),
                },
            ],
        };
        var entries = result.AsLogs();
        Assert.Single(entries);
        Assert.Equal("x", entries[0].Id);
        Assert.Equal("2026-01-01T00:00:02Z", entries[0].IngestedAt);
    }

    [Fact]
    public void WithCount_SetsIncludeTotal()
    {
        var (_, transport, bodies) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":1,"rows":[{"id":"1"}],"total_count":100,"has_more":true}""");
        using (transport)
        {
            var result = new LogQuery(transport, patientId: "p1").WithCount().Execute();
            Assert.True(bodies[0].GetProperty("include_total").GetBoolean());
            Assert.Equal(100, result.TotalCount);
            Assert.True(result.HasMore);
        }
    }

    [Fact]
    public void WithoutWithCount_TotalCountIsNull()
    {
        var (_, transport, _) = Setup(
            "/v1/state/p1/logs/query",
            """{"count":1,"rows":[{"id":"1"}]}""");
        using (transport)
        {
            var result = new LogQuery(transport, patientId: "p1").Execute();
            Assert.Null(result.TotalCount);
            Assert.Null(result.HasMore);
        }
    }

    [Fact]
    public void OpsSet_MatchesPlan()
    {
        string[] expected =
        [
            "eq", "neq", "gt", "gte", "lt", "lte", "in", "nin", "like", "ilike", "is", "exists", "contains",
        ];
        foreach (var op in expected)
        {
            using var transport = TestHelpers.CreateTransport(new MockHttpMessageHandler());
            // Filter validates against the private Ops set; unknown ops throw.
            // Known ops must not throw before Execute hits the network.
            var q = new LogQuery(transport, patientId: "p");
            var ex = Record.Exception(() => q.Filter("f", op, 1));
            Assert.Null(ex);
        }
    }
}
