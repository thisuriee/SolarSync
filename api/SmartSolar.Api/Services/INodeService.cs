/*
 * File:    INodeService.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Contract for microgrid node management. Every rule about a node —
 *          coordinate bounds, capacity bounds, the GeoJSON mapping, what a
 *          prosumer is allowed to see — lives behind this interface, so
 *          NodesController stays a routing and authorisation layer with no
 *          logic of its own.
 */
using SmartSolar.Api.Dtos;

namespace SmartSolar.Api.Services;

public interface INodeService
{
    // Lists nodes for a caller in the given role, optionally narrowed by
    // status and a name or city search.
    Task<List<NodeResponse>> FindAll(string? status, string? search, string callerRole);

    // Returns one node with its slot summary and active reservation count.
    Task<NodeDetailResponse> FindById(string id, string callerRole);

    // Registers a new node. createdByUserId comes from the caller's token.
    Task<NodeResponse> Create(NodeCreateRequest request, string createdByUserId);

    // Updates the editable fields of a node. Status, createdBy and createdAt
    // are preserved from the stored document.
    Task<NodeResponse> Update(string id, NodeUpdateRequest request);
}
