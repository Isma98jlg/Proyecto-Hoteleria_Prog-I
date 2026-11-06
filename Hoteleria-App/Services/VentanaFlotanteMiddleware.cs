using System.Net;
using System.Text.Json;

namespace Hoteleria_App.Services;

/// <summary>
/// Las páginas abiertas en una ventana flotante llevan ?modal=1. Si al terminar redirigen, se avisa
/// a la página principal en lugar de cargar el destino dentro de la ventana. El mensaje temporal
/// (TempData) sigue intacto porque esta respuesta no lo consume.
/// </summary>
public static class VentanaFlotanteMiddleware
{
    public static IApplicationBuilder UseVentanaFlotante(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        await next();

        var esRedireccion = context.Response.StatusCode is (int)HttpStatusCode.Found or (int)HttpStatusCode.SeeOther or (int)HttpStatusCode.MovedPermanently;
        if (context.Request.Query["modal"] != "1" || !esRedireccion || context.Response.HasStarted) return;

        var destino = context.Response.Headers.Location.ToString();
        context.Response.Headers.Remove("Location");
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";

        // El serializador escapa los caracteres de HTML, así que el valor es seguro dentro del script.
        var url = JsonSerializer.Serialize(destino);
        await context.Response.WriteAsync($"<!DOCTYPE html><script>parent.postMessage({{tipo:'listo',url:{url}}},location.origin);</script>");
    });
}
