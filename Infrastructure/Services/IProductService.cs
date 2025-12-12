using Domain;
using Domain.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetAllAsync();
        Task<List<ProductDto>> GetAllBranchesProducts();
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<Product> AddAsync(ProductDto productDto);
        Task<ProductDto> UpdateAsync(ProductDto product);
        Task<bool> DeleteAsync(Guid id);
    }
}
