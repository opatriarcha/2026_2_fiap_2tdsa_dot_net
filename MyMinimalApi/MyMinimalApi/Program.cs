using System.Runtime.CompilerServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();


var products = new List<Product>
{
    new(1, "Notebook", 4500),
    new( 2, "Mouse", 150),
    new(3, "Teclado", 300)
};

app.MapGet("/products", () =>
{
    return products;

});

app.Run();
    
record Product(int Id, string Name, decimal Price);

record PatchProduct(string? Name, decimal? Price);
