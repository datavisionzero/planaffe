using Planaffe.Application.Acts;
using Planaffe.Domain;

namespace Planaffe.Api.Http;

/// <param name="Approve">True confirms the login; false says the human did not start it.</param>
public sealed record DecideDeviceLoginRequest(bool? Approve);

/// <param name="DeviceCode">The long code the CLI kept when the login began.</param>
public sealed record RedeemDeviceLoginRequest(string? DeviceCode);

/// <summary>
/// The device-code login of ADR 0025: two endpoints the CLI calls with nothing
/// in its hand, and two the browser calls with the session a human already has.
/// </summary>
/// <remarks>
/// Beginning and redeeming are anonymous because the machine asking has nothing
/// yet — which is the whole point of this flow. What protects them is that a
/// begun login grants nothing until a signed-in human confirms it, and that the
/// device code carries 256 bits.
/// </remarks>
public static class DeviceLoginEndpoints
{
    public static IEndpointRouteBuilder MapDeviceLogins(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/device-logins", async (HttpContext http, BeginDeviceLogin begin,
                LoginThrottle throttle, CancellationToken ct) =>
            {
                // The one unauthenticated write that creates a row. The limit is
                // per source address and its own window, so that a machine
                // making them cannot lock a person out of signing in.
                var source = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (throttle.IsDeviceBlocked(source))
                {
                    return Problems.Result(RefusalCode.DevicePending,
                        "Too many logins were begun from here. Wait a few minutes.");
                }

                throttle.DeviceBegun(source);
                return Results.Ok(await begin.ExecuteAsync(ct));
            })
            .AllowAnonymous()
            .WithName("BeginDeviceLogin")
            .WithSummary("Begin a device login: the code the CLI prints, and the code it polls with.")
            .Produces<DeviceLoginBegun>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost("/device-logins/redeem", async (RedeemDeviceLoginRequest? request,
                RedeemDeviceLogin redeem, CancellationToken ct) =>
                Results.Ok(await redeem.ExecuteAsync(request?.DeviceCode, ct)))
            .AllowAnonymous()
            .WithName("RedeemDeviceLogin")
            .WithSummary("Collect the user token a confirmed login produced. Once, and `device-pending` until then.")
            .Produces<DeviceLoginRedeemed>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        var door = endpoints.MapGroup(string.Empty)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        door.MapGet("/device-logins/{code}", (string code, ShowDeviceLogin show, CancellationToken ct) =>
                show.ExecuteAsync(code, ct))
            .WithName("ReadDeviceLogin")
            .WithSummary("The login a typed code names, if one is still waiting. Users only.")
            .Produces<PendingDeviceLogin>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        door.MapPost("/device-logins/{code}/decide", (string code, DecideDeviceLoginRequest? request,
                DecideDeviceLogin decide, CancellationToken ct) =>
            {
                if (request?.Approve is null)
                {
                    throw Refusal.Validation("approve", "Approve is required: true confirms this login, false refuses it.");
                }

                return decide.ExecuteAsync(code, request.Approve.Value, ct);
            })
            .WithName("DecideDeviceLogin")
            .WithSummary("Confirm or refuse a device login. Users only, by the browser session they already have.")
            .Produces<PendingDeviceLogin>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        door.MapDelete("/me/token", async (RevokeMyToken revoke, CancellationToken ct) =>
            {
                await revoke.ExecuteAsync(ct);
                return Results.NoContent();
            })
            .WithName("RevokeMyToken")
            .WithSummary("Revoke the token this request presented — what `pa logout` calls.")
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }
}
