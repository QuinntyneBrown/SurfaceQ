// Acceptance Test
// Traces to: L2-024, L2-025
// Description: Sidecar `document` returns interface members (properties + methods
// with parameters and return types), enum members with computed values, type-alias
// definitions, and the contract type behind an InjectionToken.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDocumentTests
{
    [Fact]
    public void Document_returns_rich_declaration_detail()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "contract.ts");
            File.WriteAllText(
                file,
                "import { InjectionToken } from '@angular/core';\n" +
                "/** A principal. */\n" +
                "export interface Principal {\n" +
                "  /** The id. */\n" +
                "  readonly id: string;\n" +
                "  name?: string;\n" +
                "  hasRole(role: string): boolean;\n" +
                "}\n" +
                "export enum Role { Guest, Member = 10, Admin }\n" +
                "export type AuthState = string | Principal;\n" +
                "export const AUTH = new InjectionToken<Principal>('AUTH');\n");

            var result = SendDocument(file);
            var declarations = result.GetProperty("declarations").EnumerateArray().ToList();

            var iface = declarations.Single(d => d.GetProperty("name").GetString() == "Principal");
            Assert.Equal("interface", iface.GetProperty("kind").GetString());
            Assert.Equal("A principal.", iface.GetProperty("doc").GetString());
            var members = iface.GetProperty("members").EnumerateArray().ToList();

            var id = members.Single(m => m.GetProperty("name").GetString() == "id");
            Assert.Equal("property", id.GetProperty("memberKind").GetString());
            Assert.Equal("string", id.GetProperty("type").GetString());
            Assert.True(id.GetProperty("readonly").GetBoolean());
            Assert.False(id.GetProperty("optional").GetBoolean());
            Assert.Equal("The id.", id.GetProperty("doc").GetString());

            var name = members.Single(m => m.GetProperty("name").GetString() == "name");
            Assert.True(name.GetProperty("optional").GetBoolean());

            var hasRole = members.Single(m => m.GetProperty("name").GetString() == "hasRole");
            Assert.Equal("method", hasRole.GetProperty("memberKind").GetString());
            Assert.Equal("boolean", hasRole.GetProperty("returnType").GetString());
            var param = hasRole.GetProperty("parameters").EnumerateArray().Single();
            Assert.Equal("role", param.GetProperty("name").GetString());
            Assert.Equal("string", param.GetProperty("type").GetString());

            var role = declarations.Single(d => d.GetProperty("name").GetString() == "Role");
            var roleMembers = role.GetProperty("members").EnumerateArray().ToList();
            Assert.Equal("0", roleMembers[0].GetProperty("value").GetString());
            Assert.Equal("10", roleMembers[1].GetProperty("value").GetString());
            Assert.Equal("11", roleMembers[2].GetProperty("value").GetString());

            var alias = declarations.Single(d => d.GetProperty("name").GetString() == "AuthState");
            Assert.Equal("type", alias.GetProperty("kind").GetString());
            Assert.Equal("string | Principal", alias.GetProperty("definition").GetString());

            var token = declarations.Single(d => d.GetProperty("name").GetString() == "AUTH");
            Assert.Equal("injection-token", token.GetProperty("kind").GetString());
            Assert.Equal("Principal", token.GetProperty("contract").GetString());
            Assert.Equal("AUTH", token.GetProperty("description").GetString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Document_infers_property_type_from_initializer_type_argument()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "widget.ts");
            // Angular signal pattern: the value type lives in the call's type
            // argument, not an explicit annotation. computed() has no type
            // argument and must stay blank rather than dumping the arrow body.
            File.WriteAllText(
                file,
                "export class Widget {\n" +
                "  variant = input<ButtonVariant>('primary');\n" +
                "  item = input.required<Item>();\n" +
                "  clicked = output<Event>();\n" +
                "  value = signal<string | null>(null);\n" +
                "  label = computed(() => this.variant());\n" +
                "}\n");

            var result = SendDocument(file);
            var members = result.GetProperty("declarations").EnumerateArray().Single()
                .GetProperty("members").EnumerateArray().ToList();
            string TypeOf(string name) => members
                .Single(m => m.GetProperty("name").GetString() == name)
                .GetProperty("type").GetString()!;

            Assert.Equal("ButtonVariant", TypeOf("variant"));
            Assert.Equal("Item", TypeOf("item"));
            Assert.Equal("Event", TypeOf("clicked"));
            Assert.Equal("string | null", TypeOf("value"));
            Assert.Equal("", TypeOf("label"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Document_reports_parse_errors()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "broken.ts");
            File.WriteAllText(file, "export interface Broken {\n");

            var result = SendDocument(file);

            Assert.NotEmpty(result.GetProperty("errors").EnumerateArray());
            Assert.Empty(result.GetProperty("declarations").EnumerateArray());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static JsonElement SendDocument(string file)
    {
        using var client = new SidecarClient(SidecarScript.ResolvePath());
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "document",
            @params = new { file },
        });
        var responseJson = client.Send(request);
        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("result").Clone();
    }
}
