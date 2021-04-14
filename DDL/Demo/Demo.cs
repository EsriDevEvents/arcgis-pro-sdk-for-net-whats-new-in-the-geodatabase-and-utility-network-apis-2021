using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Hosting;
using System;
using System.Collections.Generic;

namespace Demo
{
  class Program
  {
    //[STAThread] must be present on the Application entry point
    [STAThread]
    static void Main(string[] args)
    {
      //Call Host.Initialize before constructing any objects from ArcGIS.Core
      Host.Initialize();
      //TODO: Add your business logic here.

      string tableName = "TestTable";

      // Create the table
      CreateTable(tableName);

      // Delete the table
      DeleteTable(tableName);
    }

    static void CreateTable(string tableName)
    {
      // Create a list of 2 FieldDescription objects

      FieldDescription objectIDFieldDescription = FieldDescription.CreateObjectIDField();

      FieldDescription textFieldDescription = new FieldDescription("TextField", FieldType.String)
      {
        AliasName = "TextField_Alias",
        Length = 100
      };

      IReadOnlyList<FieldDescription> fieldDescriptions = new List<FieldDescription>()
      {
        objectIDFieldDescription,
        textFieldDescription
      };

      TableDescription tableDescription = new TableDescription(tableName, fieldDescriptions);

      string geodatabasePath = @"C:\Demo\Demo.gdb";
      FileGeodatabaseConnectionPath fileGeodatabaseConnectionPath = new FileGeodatabaseConnectionPath(new Uri(geodatabasePath));

      using (Geodatabase geodatabase = new Geodatabase(fileGeodatabaseConnectionPath))
      {
        SchemaBuilder schemaBuilder = new SchemaBuilder(geodatabase);

        // Queue the operation to create the table
        schemaBuilder.Create(tableDescription);

        // Run all queued operations
        bool success = schemaBuilder.Build();

        if (success)
        {
          System.Console.WriteLine($"Successfully created {tableDescription.Name}");
        }
        else
        {
          for (int i = 0; i < schemaBuilder.ErrorMessages.Count; ++i)
          {
            System.Console.WriteLine(schemaBuilder.ErrorMessages[i]);
          }
        }
      }
    }

    static void DeleteTable(string tableName)
    {
      string geodatabasePath = @"C:\Demo\Demo.gdb";
      FileGeodatabaseConnectionPath fileGeodatabaseConnectionPath = new FileGeodatabaseConnectionPath(new Uri(geodatabasePath));

      // Open the Geodatabase and get the TableDefinition

      using (Geodatabase geodatabase = new Geodatabase(fileGeodatabaseConnectionPath))
      using (TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(tableName))
      {
        SchemaBuilder schemaBuilder = new SchemaBuilder(geodatabase);

        TableDescription tableDescription = new TableDescription(tableDefinition);

        schemaBuilder.Delete(tableDescription);

        bool success = schemaBuilder.Build();

        if (success)
        {
          System.Console.WriteLine($"Successfully deleted {tableDescription.Name}");
        }
        else
        {
          for (int i = 0; i < schemaBuilder.ErrorMessages.Count; ++i)
          {
            System.Console.WriteLine(schemaBuilder.ErrorMessages[i]);
          }
        }
      }
    }

  }
}
