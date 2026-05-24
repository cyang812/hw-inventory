using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HwInventory.Api.Grpc;

namespace HwInventory.Api.Grpc;

/// <summary>
/// Minimal gRPC health service. The rest of the gRPC mirror (Hardware, Project,
/// Category, Tag, Activity, Dashboard, ImportExport) is intentionally deferred to
/// a follow-up — it is a 1:1 wrapper over the existing service layer.
/// </summary>
public class HealthGrpcService : HealthService.HealthServiceBase
{
    public override Task<HealthResponse> Check(Empty request, ServerCallContext context) =>
        Task.FromResult(new HealthResponse { Status = "ok" });
}
