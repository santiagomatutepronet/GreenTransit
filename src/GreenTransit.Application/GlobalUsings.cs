// Usings globales para la capa Application.
// Microsoft.EntityFrameworkCore se incluye globalmente porque los query handlers
// utilizan sus métodos de extensión sobre IQueryable<T>:
// AsNoTracking, Include, ThenInclude, ToListAsync, FirstOrDefaultAsync, etc.
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
