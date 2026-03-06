using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<CustomerData>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapGet("/customers", async (CustomerData data) =>
{
    var customers = await data.ListAsync();
    return TypedResults.Ok(customers);
})
.WithName("ListCustomers")
.WithOpenApi();

app.MapGet("/customers/{id:guid}", async (Guid id, CustomerData data) =>
    await data.GetByIdAsync(id) is Customer customer
        ? TypedResults.Ok(customer)
        : Results.NotFound()
)
.WithName("GetCustomerById")
.WithOpenApi();

app.MapPost("/customers", async (Customer customer, CustomerData data) =>
{
    var newCustomer = customer with { Id = Guid.NewGuid(), Projects = new List<Project>() };

    await data.AddAsync(newCustomer);
    return Results.Created($"/customers/{newCustomer.Id}", newCustomer);
})
.AddEndpointFilter<ValidateCustomer>()
.WithName("AddCustomer")
.WithOpenApi();



app.MapPut("/customers/{id:guid}", async ([AsParameters] PutRequest request) =>
{
    Customer? existingCustomer = await request.Data.GetByIdAsync(request.Id);
   
    if(existingCustomer is null) return Results.NotFound();


    Customer updatedCustomer = existingCustomer with
    {
        CompanyName = request.Customer.CompanyName,
        Projects = request.Customer.Projects ?? new List<Project>()
    };

    await request.Data.UpdateAsync(request.Customer);
    return Results.Ok(updatedCustomer);
})
//.AddEndpointFilter<ValidateCustomer>()
.WithParameterValidation()
.WithName("UpdateCustomer")
.WithOpenApi();

app.MapDelete("/customers/{id:guid}", async (Guid id,  CustomerData data) =>
{
    if (await data.GetByIdAsync(id) is null) return Results.NotFound();
    await data.DeleteAsync(id);
    return Results.NoContent();

})
.WithName("DeleteCustomer")
.WithOpenApi();


app.Run();



public record Customer(Guid Id, [MinLength(10)]string CompanyName, List<Project> Projects);
public record Project(Guid Id, string ProjectName, Guid CustomerId);

public readonly record struct PutRequest : IValidatableObject
{
    [FromRoute(Name = "id")]
    [Required]
    public Guid Id { get; init; }

    [Required]
    public Customer Customer { get; init; }

    public CustomerData Data { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.IsNullOrEmpty(Customer.CompanyName)) 
        { 
            yield return new ValidationResult("CompanyName is required.", new[] { nameof(Customer.CompanyName) });
        }
    }
}

public class CustomerData
{
    private readonly Guid _customer1Id = Guid.NewGuid();
    private readonly Guid _customer2Id = Guid.NewGuid(); 
    private readonly List<Customer> _customers;

    public CustomerData()
    {
        _customers = new List<Customer>
        {
            new Customer(_customer1Id, "Acme", new List<Project>
            {
                new Project(Guid.NewGuid(), "Project 1", _customer1Id),
                new Project(Guid.NewGuid(), "Project 2", _customer1Id)
            }),
             new Customer(_customer2Id, "Contoso", new List<Project>
            {
                new Project(Guid.NewGuid(), "Project 3", _customer2Id),
                new Project(Guid.NewGuid(), "Project 4", _customer2Id)
            })
        };
    }

    public Task<List<Customer>> ListAsync()
    {
        return Task.FromResult(_customers);
    }

    public Task<Customer?> GetByIdAsync(Guid id) {
        return Task.FromResult(_customers.FirstOrDefault(c => c.Id == id));
    }


    public Task AddAsync(Customer newCustomer) 
    { 
        _customers.Add(newCustomer);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer customer)
    {
        if (_customers.Any(c => c.Id == customer.Id)) 
        {
            var index = _customers.FindIndex(c => c.Id == customer.Id);
            _customers[index] = customer;
        }
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid id)
    {
        if (_customers.Any(c => c.Id == id))
        {
            var index = _customers.FindIndex(c => c.Id == id);
            _customers.RemoveAt(index);
        }
        return Task.CompletedTask;
    }
}

public class ValidateCustomer : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) 
    { 
        var customer = context.Arguments.FirstOrDefault(a => a is Customer) as Customer;

        if (customer is not null && string.IsNullOrWhiteSpace(customer.CompanyName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            {"CompanyName", new[] { "CompanyName is required." } }
        });
        }

        return await next(context);
    }
}