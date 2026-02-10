using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(Guid id);
        Task<Product> AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task<bool> DeleteAsync(Guid id); // Retorna bool si tuvo éxito
    }
}