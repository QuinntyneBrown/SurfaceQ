// Acceptance Test
// Traces to: L2-040
// Description: ProviderGenerator wires each InjectionToken<IFoo> to the exported
// class that implements IFoo (useExisting) and binds config tokens through a
// generated options object (useValue), at the common-ancestor directory with
// correct relative imports — deterministically and LF-terminated. A token whose
// interface has no implementation is warned and skipped.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class ProviderGeneratorTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pg-fixture");

    [Fact]
    public void Wires_service_bindings_and_config_options()
    {
        var api = new LibraryApi("api", new[]
        {
            Token("API_BASE_URL", "string", F("src/lib/api-base-url.token.ts")),
            Iface("IBillsService", F("src/lib/services/bills.service.contract.ts")),
            Token("BILLS_SERVICE", "IBillsService", F("src/lib/services/bills.service.contract.ts")),
            Cls("BillsService", F("src/lib/services/bills.service.ts"), "IBillsService"),
        });

        var result = new ProviderGenerator().Generate(api, "api");

        Assert.Equal("provideApi", result.FunctionName);
        Assert.EndsWith("provide-api.ts", result.TargetFile.Replace('\\', '/'));
        Assert.Equal(2, result.BindingCount);
        Assert.Empty(result.Warnings);

        var c = result.Content;
        Assert.Contains("import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';", c);
        Assert.Contains("import { API_BASE_URL } from './api-base-url.token';", c);
        Assert.Contains("import { BILLS_SERVICE } from './services/bills.service.contract';", c);
        Assert.Contains("import { BillsService } from './services/bills.service';", c);
        Assert.Contains("export interface ProvideApiOptions {", c);
        Assert.Contains("  readonly apiBaseUrl: string;", c);
        Assert.Contains("export function provideApi(options: ProvideApiOptions): EnvironmentProviders {", c);
        Assert.Contains("{ provide: API_BASE_URL, useValue: options.apiBaseUrl },", c);
        Assert.Contains("{ provide: BILLS_SERVICE, useExisting: BillsService },", c);
    }

    [Fact]
    public void Omits_the_options_parameter_when_there_are_no_config_tokens()
    {
        var api = new LibraryApi("api", new[]
        {
            Iface("IBillsService", F("src/lib/bills.contract.ts")),
            Token("BILLS_SERVICE", "IBillsService", F("src/lib/bills.contract.ts")),
            Cls("BillsService", F("src/lib/bills.service.ts"), "IBillsService"),
        });

        var c = new ProviderGenerator().Generate(api, "api").Content;

        Assert.Contains("export function provideApi(): EnvironmentProviders {", c);
        Assert.DoesNotContain("ProvideApiOptions", c);
        Assert.DoesNotContain("useValue", c);
    }

    [Fact]
    public void Warns_and_skips_a_token_whose_interface_has_no_implementation()
    {
        var api = new LibraryApi("api", new[]
        {
            Iface("INotesService", F("src/lib/notes.contract.ts")),
            Token("NOTES_SERVICE", "INotesService", F("src/lib/notes.contract.ts")),
        });

        var result = new ProviderGenerator().Generate(api, "api");

        Assert.Equal(0, result.BindingCount);
        Assert.Equal("", result.Content);
        Assert.Contains(result.Warnings, w => w.Contains("NOTES_SERVICE") && w.Contains("no implementation"));
    }

    [Fact]
    public void Derives_the_function_and_file_name_from_the_project_name()
    {
        var api = new LibraryApi("foo-bar", new[]
        {
            Token("API_BASE_URL", "string", F("src/lib/token.ts")),
        });

        var result = new ProviderGenerator().Generate(api, "foo-bar");

        Assert.Equal("provideFooBar", result.FunctionName);
        Assert.EndsWith("provide-foo-bar.ts", result.TargetFile.Replace('\\', '/'));
        Assert.Contains("export interface ProvideFooBarOptions {", result.Content);
    }

    [Fact]
    public void Output_is_deterministic_and_lf_terminated()
    {
        var api = new LibraryApi("api", new[]
        {
            Token("API_BASE_URL", "string", F("src/lib/token.ts")),
            Iface("IBillsService", F("src/lib/bills.contract.ts")),
            Token("BILLS_SERVICE", "IBillsService", F("src/lib/bills.contract.ts")),
            Cls("BillsService", F("src/lib/bills.service.ts"), "IBillsService"),
        });
        var generator = new ProviderGenerator();

        var first = generator.Generate(api, "api");
        var second = generator.Generate(api, "api");

        Assert.Equal(first.Content, second.Content);
        Assert.DoesNotContain('\r', first.Content);
        Assert.EndsWith("\n", first.Content);
    }

    private static string F(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static ApiDeclaration Iface(string name, string file) =>
        new(name, "interface", "", "", "", "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ApiParameter>(), Array.Empty<ApiMember>(), Array.Empty<EnumMember>(), false, "", file);

    private static ApiDeclaration Cls(string name, string file, params string[] implements) =>
        new(name, "class", "", "", "", "", "", "", Array.Empty<string>(), implements,
            Array.Empty<ApiParameter>(), Array.Empty<ApiMember>(), Array.Empty<EnumMember>(), false, "", file);

    private static ApiDeclaration Token(string name, string contract, string file) =>
        new(name, "injection-token", "", "", contract, "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ApiParameter>(), Array.Empty<ApiMember>(), Array.Empty<EnumMember>(), false, "", file);
}
