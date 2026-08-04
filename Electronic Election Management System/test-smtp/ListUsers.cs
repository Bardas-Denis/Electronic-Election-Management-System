using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        using var connection = new SqliteConnection("Data Source=../election.db");
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Email FROM Users";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine(reader.GetString(0));
        }
    }
}
