using System.Text.Json;
using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Models;

public class SampleDataTests
{
    // ── ToNested ──

    [Fact]
    public void ToNested_SingleKey_ReturnsFlat()
    {
        var data = new SampleData();
        data.FlatData["name"] = "Alice";

        var nested = data.ToNested();

        nested.Should().ContainKey("name");
        nested["name"].Should().Be("Alice");
    }

    [Fact]
    public void ToNested_DottedKey_CreatesNestedDictionary()
    {
        var data = new SampleData();
        data.FlatData["variables.firstName"] = "Jane";

        var nested = data.ToNested();

        nested.Should().ContainKey("variables");
        var variables = nested["variables"].Should().BeOfType<Dictionary<string, object>>().Subject;
        variables.Should().ContainKey("firstName");
        variables["firstName"].Should().Be("Jane");
    }

    [Fact]
    public void ToNested_DeepNesting_CreatesMultipleLevels()
    {
        var data = new SampleData();
        data.FlatData["a.b.c.d"] = "deep";

        var nested = data.ToNested();

        var a = nested["a"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var b = a["b"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var c = b["c"].Should().BeOfType<Dictionary<string, object>>().Subject;
        c["d"].Should().Be("deep");
    }

    [Fact]
    public void ToNested_MultipleSiblingKeys_GroupsCorrectly()
    {
        var data = new SampleData();
        data.FlatData["variables.firstName"] = "Jane";
        data.FlatData["variables.lastName"] = "Smith";
        data.FlatData["branding.colour"] = "#fff";

        var nested = data.ToNested();

        nested.Should().ContainKeys("variables", "branding");
        var variables = (Dictionary<string, object>)nested["variables"];
        variables["firstName"].Should().Be("Jane");
        variables["lastName"].Should().Be("Smith");
        var branding = (Dictionary<string, object>)nested["branding"];
        branding["colour"].Should().Be("#fff");
    }

    [Fact]
    public void ToNested_EmptyDictionary_ReturnsEmpty()
    {
        var data = new SampleData();
        var nested = data.ToNested();
        nested.Should().BeEmpty();
    }

    [Fact]
    public void ToNested_CaseInsensitive_MergesKeys()
    {
        var data = new SampleData();
        data.FlatData["Variables.FirstName"] = "Jane";
        data.FlatData["variables.lastName"] = "Smith";

        var nested = data.ToNested();

        // Should merge under same key (case-insensitive)
        nested.Should().HaveCount(1);
        var variables = (Dictionary<string, object>)nested.Values.First();
        variables.Should().HaveCount(2);
    }

    // ── FromNested ──

    [Fact]
    public void FromNested_FlatDictionary_ProducesSimpleKeys()
    {
        var nested = new Dictionary<string, object>
        {
            ["name"] = "Alice",
            ["age"] = "30"
        };

        var data = SampleData.FromNested(nested);

        data.FlatData.Should().ContainKey("name").WhoseValue.Should().Be("Alice");
        data.FlatData.Should().ContainKey("age").WhoseValue.Should().Be("30");
    }

    [Fact]
    public void FromNested_NestedDictionary_ProducesDottedKeys()
    {
        var nested = new Dictionary<string, object>
        {
            ["variables"] = new Dictionary<string, object>
            {
                ["firstName"] = "Jane",
                ["lastName"] = "Smith"
            }
        };

        var data = SampleData.FromNested(nested);

        data.FlatData.Should().ContainKey("variables.firstName").WhoseValue.Should().Be("Jane");
        data.FlatData.Should().ContainKey("variables.lastName").WhoseValue.Should().Be("Smith");
    }

    [Fact]
    public void FromNested_NullInput_Throws()
    {
        var act = () => SampleData.FromNested(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromNested_EmptyDictionary_ReturnsEmptyFlatData()
    {
        var data = SampleData.FromNested(new Dictionary<string, object>());
        data.FlatData.Should().BeEmpty();
    }

    [Fact]
    public void FromNested_NullValue_StoredAsEmptyString()
    {
        var nested = new Dictionary<string, object>
        {
            ["key"] = null!
        };

        var data = SampleData.FromNested(nested);
        data.FlatData["key"].Should().Be(string.Empty);
    }

    // ── RoundTrip: Flat → Nested → Flat ──

    [Fact]
    public void RoundTrip_PreservesAllKeys()
    {
        var original = new SampleData();
        original.FlatData["branding.companyName"] = "TechConf";
        original.FlatData["branding.primaryColour"] = "#6C3CE1";
        original.FlatData["variables.firstName"] = "Jane";
        original.FlatData["variables.lastName"] = "Smith";

        var nested = original.ToNested();
        var roundTripped = SampleData.FromNested(nested);

        roundTripped.FlatData.Should().HaveCount(4);
        roundTripped.FlatData["branding.companyName"].Should().Be("TechConf");
        roundTripped.FlatData["branding.primaryColour"].Should().Be("#6C3CE1");
        roundTripped.FlatData["variables.firstName"].Should().Be("Jane");
        roundTripped.FlatData["variables.lastName"].Should().Be("Smith");
    }

    // ── FromJsonElement ──

    [Fact]
    public void FromJsonElement_FlatObject_ParsesCorrectly()
    {
        var json = """{"name":"Alice","age":30}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["name"].Should().Be("Alice");
        data.FlatData["age"].Should().Be("30");
    }

    [Fact]
    public void FromJsonElement_NestedObject_ProducesDottedKeys()
    {
        var json = """{"variables":{"firstName":"Jane","lastName":"Smith"}}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["variables.firstName"].Should().Be("Jane");
        data.FlatData["variables.lastName"].Should().Be("Smith");
    }

    [Fact]
    public void FromJsonElement_Boolean_StoredAsLowercaseString()
    {
        var json = """{"active":true,"deleted":false}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["active"].Should().Be("true");
        data.FlatData["deleted"].Should().Be("false");
    }

    [Fact]
    public void FromJsonElement_Null_StoredAsEmptyString()
    {
        var json = """{"value":null}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["value"].Should().Be(string.Empty);
    }

    [Fact]
    public void FromJsonElement_Array_StoredAsRawJson()
    {
        var json = """{"tags":["a","b","c"]}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["tags"].Should().Be("""["a","b","c"]""");
    }

    [Fact]
    public void FromJsonElement_Number_StoredAsRawText()
    {
        var json = """{"price":19.99}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData["price"].Should().Be("19.99");
    }

    [Fact]
    public void FromJsonElement_DeeplyNested_FlattensFully()
    {
        var json = """{"a":{"b":{"c":"deep"}}}""";
        var element = JsonDocument.Parse(json).RootElement;

        var data = SampleData.FromJsonElement(element);

        data.FlatData.Should().ContainKey("a.b.c").WhoseValue.Should().Be("deep");
    }

    // ── DefaultSampleData ──

    [Fact]
    public void DefaultSampleData_HasExpectedKeys()
    {
        var defaults = SampleData.DefaultSampleData;

        defaults.FlatData.Should().ContainKey("branding.companyName");
        defaults.FlatData.Should().ContainKey("branding.primaryColour");
        defaults.FlatData.Should().ContainKey("variables.firstName");
        defaults.FlatData.Should().ContainKey("variables.lastName");
        defaults.FlatData.Should().ContainKey("variables.attendeeId");
        defaults.FlatData.Count.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void DefaultSampleData_IsNewInstanceEachCall()
    {
        var a = SampleData.DefaultSampleData;
        var b = SampleData.DefaultSampleData;

        a.Should().NotBeSameAs(b);
    }
}
