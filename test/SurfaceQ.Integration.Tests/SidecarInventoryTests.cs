// Acceptance Test
// Traces to: L2-033
// Description: Sidecar `inventory` reports every top-level declaration (exported
// and internal) with its TypeScript kind, an `exported` flag, and an Angular
// role detected from decorators, class heritage, or functional types.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarInventoryTests
{
    [Fact]
    public void Inventory_reports_exported_and_internal_declarations()
    {
        var file = WriteTemp(
            "export class Exported {}\n" +
            "class Internal {}\n" +
            "export interface Shape { x: number; }\n" +
            "interface Hidden { y: number; }\n" +
            "export enum Color { Red, Green }\n" +
            "export type Id = string;\n" +
            "export function helper(): void {}\n" +
            "function privateHelper(): void {}\n" +
            "export const COUNT = 1;\n" +
            "const local = 2;\n");
        try
        {
            var items = Inventory(file);

            bool Exported(string name) => Item(items, name).GetProperty("exported").GetBoolean();
            string Kind(string name) => Item(items, name).GetProperty("kind").GetString()!;

            Assert.True(Exported("Exported"));
            Assert.False(Exported("Internal"));
            Assert.True(Exported("Shape"));
            Assert.False(Exported("Hidden"));
            Assert.False(Exported("privateHelper"));
            Assert.False(Exported("local"));

            Assert.Equal("class", Kind("Exported"));
            Assert.Equal("interface", Kind("Shape"));
            Assert.Equal("enum", Kind("Color"));
            Assert.Equal("type", Kind("Id"));
            Assert.Equal("function", Kind("helper"));
            Assert.Equal("const", Kind("COUNT"));
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_classifies_friendly_roles_for_plain_kinds()
    {
        var file = WriteTemp(
            "export class Plain {}\n" +
            "export interface Shape { x: number; }\n" +
            "export enum Color { Red }\n" +
            "export type Id = string;\n" +
            "export function helper(): void {}\n" +
            "export const COUNT = 1;\n");
        try
        {
            var items = Inventory(file);
            string Role(string name) => Item(items, name).GetProperty("role").GetString()!;

            Assert.Equal("Class", Role("Plain"));
            Assert.Equal("Interface", Role("Shape"));
            Assert.Equal("Enum", Role("Color"));
            Assert.Equal("Type Alias", Role("Id"));
            Assert.Equal("Function", Role("helper"));
            Assert.Equal("Constant", Role("COUNT"));
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_detects_angular_decorator_roles()
    {
        var file = WriteTemp(
            "import { Component, Directive, Pipe, Injectable, NgModule } from '@angular/core';\n" +
            "@Component({ selector: 'app-root', template: '' })\n" +
            "export class AppComponent {}\n" +
            "@Directive({ selector: '[appHighlight]' })\n" +
            "export class HighlightDirective {}\n" +
            "@Pipe({ name: 'cap' })\n" +
            "export class CapPipe {}\n" +
            "@Injectable({ providedIn: 'root' })\n" +
            "export class UserService {}\n" +
            "@NgModule({})\n" +
            "export class AppModule {}\n");
        try
        {
            var items = Inventory(file);
            string Role(string name) => Item(items, name).GetProperty("role").GetString()!;

            Assert.Equal("Component", Role("AppComponent"));
            Assert.Equal("Directive", Role("HighlightDirective"));
            Assert.Equal("Pipe", Role("CapPipe"));
            Assert.Equal("Service", Role("UserService"));
            Assert.Equal("NgModule", Role("AppModule"));
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_detects_guard_resolver_interceptor_roles()
    {
        var file = WriteTemp(
            "export const authGuard: CanActivateFn = () => true;\n" +
            "export const altGuard = (() => true) as CanActivateFn;\n" +
            "export const userResolver: ResolveFn<User> = () => ({} as User);\n" +
            "export const authInterceptor: HttpInterceptorFn = (req, next) => next(req);\n" +
            "export class LegacyGuard implements CanActivate { canActivate() { return true; } }\n" +
            "export class LegacyResolver implements Resolve<User> { resolve() { return {} as User; } }\n" +
            "@Injectable() export class AdminGuard implements CanActivate { canActivate() { return true; } }\n" +
            "@Injectable() export class TokenInterceptor implements HttpInterceptor { intercept(r: any, n: any) { return n.handle(r); } }\n");
        try
        {
            var items = Inventory(file);
            string Role(string name) => Item(items, name).GetProperty("role").GetString()!;

            Assert.Equal("Guard", Role("authGuard"));
            // The `(...) as CanActivateFn` form carries its type on the initializer.
            Assert.Equal("Guard", Role("altGuard"));
            Assert.Equal("Resolver", Role("userResolver"));
            Assert.Equal("Interceptor", Role("authInterceptor"));
            Assert.Equal("Guard", Role("LegacyGuard"));
            Assert.Equal("Resolver", Role("LegacyResolver"));
            // Heritage role outranks @Injectable for guards and interceptors alike.
            Assert.Equal("Guard", Role("AdminGuard"));
            Assert.Equal("Interceptor", Role("TokenInterceptor"));
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_detects_namespaced_decorator_roles()
    {
        var file = WriteTemp(
            "import * as ng from '@angular/core';\n" +
            "@ng.Component({ selector: 'x', template: '' })\n" +
            "export class NsComponent {}\n");
        try
        {
            var items = Inventory(file);
            Assert.Equal("Component", Item(items, "NsComponent").GetProperty("role").GetString());
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_detects_injection_tokens()
    {
        var file = WriteTemp(
            "import { InjectionToken } from '@angular/core';\n" +
            "export const API_URL = new InjectionToken<string>('API_URL');\n");
        try
        {
            var items = Inventory(file);
            var token = Item(items, "API_URL");
            Assert.Equal("Injection Token", token.GetProperty("role").GetString());
            Assert.Equal("const", token.GetProperty("kind").GetString());
            Assert.True(token.GetProperty("exported").GetBoolean());
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    [Fact]
    public void Inventory_reports_parse_errors()
    {
        var file = WriteTemp("export class Broken {\n");
        try
        {
            using var client = new SidecarClient(SidecarScript.ResolvePath());
            var result = Send(client, file);
            Assert.NotEmpty(result.GetProperty("errors").EnumerateArray());
            Assert.Empty(result.GetProperty("items").EnumerateArray());
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    private static List<JsonElement> Inventory(string file)
    {
        using var client = new SidecarClient(SidecarScript.ResolvePath());
        return Send(client, file).GetProperty("items").EnumerateArray().ToList();
    }

    private static JsonElement Send(SidecarClient client, string file)
    {
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "inventory",
            @params = new { file },
        });
        return JsonDocument.Parse(client.Send(request)).RootElement.GetProperty("result").Clone();
    }

    private static JsonElement Item(List<JsonElement> items, string name) =>
        items.Single(i => i.GetProperty("name").GetString() == name);

    private static string WriteTemp(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "source.ts");
        File.WriteAllText(file, source);
        return file;
    }

    private static void DeleteTemp(string file) =>
        Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
}
