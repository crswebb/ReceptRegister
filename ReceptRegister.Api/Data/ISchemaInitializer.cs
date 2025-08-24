namespace ReceptRegister.Api.Data;

public interface ISchemaInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
