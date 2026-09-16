using AuthMicroservice.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AuthMicroservice.UnitTests.Configuration;

public class EndpointToggleBindingTests
{
    private static EndpointOptions Bind(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new EndpointOptions();
        config.GetSection("Endpoints").Bind(options);
        return options;
    }

    [Fact]
    public void ObjectForm_BindsEnabledAndShowInSwagger()
    {
        var opts = Bind(new Dictionary<string, string?>
        {
            ["Endpoints:Register:Enabled"] = "false",
            ["Endpoints:Register:ShowInSwagger"] = "false"
        });

        opts.Register.Enabled.Should().BeFalse();
        opts.Register.ShowInSwagger.Should().BeFalse();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ShorthandForm_MapsScalarToEnabled(string raw, bool expected)
    {
        var opts = Bind(new Dictionary<string, string?>
        {
            ["Endpoints:Register"] = raw
        });

        opts.Register.Enabled.Should().Be(expected);
        opts.Register.ShowInSwagger.Should().BeTrue();
    }

    [Fact]
    public void Unspecified_KeepsDefaults()
    {
        var opts = Bind(new Dictionary<string, string?>());

        opts.Register.Enabled.Should().BeTrue();
        opts.Register.ShowInSwagger.Should().BeTrue();
    }

    [Fact]
    public void MixedForms_BindIndependently()
    {
        var opts = Bind(new Dictionary<string, string?>
        {
            ["Endpoints:Register"] = "false",
            ["Endpoints:Health:Enabled"] = "true",
            ["Endpoints:Health:ShowInSwagger"] = "false"
        });

        opts.Register.Enabled.Should().BeFalse();
        opts.Health.Enabled.Should().BeTrue();
        opts.Health.ShowInSwagger.Should().BeFalse();
    }
}
