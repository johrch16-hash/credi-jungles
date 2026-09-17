using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var connectionString = "Data Source=../PrestamosCobros.Web/prestamos.db";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            try
            {
                command.CommandText = "ALTER TABLE RegistrosVentas ADD COLUMN FechaSorteo TEXT;";
                command.ExecuteNonQuery();
                Console.WriteLine("Columna FechaSorteo añadida a RegistrosVentas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error FechaSorteo: " + ex.Message);
            }
        }
        
        Console.WriteLine("Actualización finalizada.");
    }
}
