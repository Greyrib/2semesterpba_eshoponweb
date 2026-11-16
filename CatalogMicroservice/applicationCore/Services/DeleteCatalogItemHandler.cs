using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using Messaging;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Services;

public class DeleteCatalogItemHandler : IRequestHandler<DeleteCatalogItemRequest, bool>
{
    private readonly IRepository<CatalogItem> _itemRepository;

    public DeleteCatalogItemHandler(IRepository<CatalogItem> itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<bool> HandleAsync(DeleteCatalogItemRequest request, CancellationToken ct)
    {
        var itemToDelete = await _itemRepository.GetByIdAsync(request.CatalogItemId, ct);
        if (itemToDelete is null) return false;

        await _itemRepository.DeleteAsync(itemToDelete, ct);
        return true;
    }
}
