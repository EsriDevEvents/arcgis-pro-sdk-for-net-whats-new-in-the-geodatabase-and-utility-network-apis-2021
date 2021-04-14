using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Version = ArcGIS.Core.Data.Version;

namespace VersionManagementDemo
{
  public class VersionOperationsService
  {

    private readonly AlertService _alertService;
    public VersionOperationsService(AlertService alertService)
    {
      _alertService = alertService;
    }

    #region Creating versions

    public async Task<string> CreateVersionAsync(string versionName)
    {
      string newVersionName = null;
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase?.GetVersionManager())
          {
            Version defaultVersion = versionManager?.GetVersions().FirstOrDefault(version =>
            {
              string name = version.GetName();
              return name.ToLowerInvariant().Equals("dbo.default") || name.ToLowerInvariant().Equals("sde.default");
            });
            
            if (defaultVersion is null) return;
            using (defaultVersion)
            {
              VersionDescription newVersionDescription = new VersionDescription(versionName, 
                                                                                $"{versionName} description", 
                                                                                VersionAccessType.Public);

              using (Version newVersion = versionManager.CreateVersion(newVersionDescription, defaultVersion))
              {
                newVersionName = newVersion.GetName();
              }
            }

          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
      return newVersionName;
    }

    #endregion

    #region Switching versions

    public async Task ChangeVersionAsync(string toVersionName)
    {
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase?.GetVersionManager())
          using (Version currentVersion = versionManager?.GetCurrentVersion())
          using (Version toVersion = versionManager?.GetVersions().First(version => version.GetName().Contains(toVersionName)))
          {
            if (currentVersion != null && toVersion != null)
            {
              if(currentVersion != toVersion)
              {
                MapView.Active.Map.ChangeVersion(currentVersion, toVersion);
              }
            }
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
    }

    public async Task ResetToDefaultVersionAsync()
    {
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase?.GetVersionManager())
          using (Version currentVersion = versionManager?.GetCurrentVersion())
          {
            Version defaultVersion = versionManager?.GetVersions().FirstOrDefault(version =>
            {
              using (Version parent = version.GetParent())
              {
                bool isDefaultVersion = parent == null;
                return isDefaultVersion;
              }
            });

            if (currentVersion != null && defaultVersion != null && currentVersion.GetName() != defaultVersion.GetName())
            {
              using (defaultVersion)
              {
                MapView.Active.Map.ChangeVersion(currentVersion, defaultVersion);
              }
            }
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
    }

    #endregion

    #region Posting and Reconciling
    public async Task PostAsync(IEnumerable<string> selectedDeletedFeatures)
    {
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase.GetVersionManager())
          using (Version currentVersion = versionManager.GetCurrentVersion())
          using (Version parentVersion = currentVersion.GetParent())
          {
            // Create a Post ReconcileDescription
            ReconcileDescription reconcileDescription = new ReconcileDescription(parentVersion)
            {
              ConflictResolutionMethod = ConflictResolutionMethod.Continue,   // continue if conflicts are found
              ConflictDetectionType = ConflictDetectionType.ByRow,
              ConflictResolutionType = ConflictResolutionType.FavorEditVersion,
              WithPost = true
            };


            List<Selection> selectionsForPartialPosting = await GetSelectedFeaturesForPartialPostingAsync(selectedDeletedFeatures);
            if (selectionsForPartialPosting?.Count > 0)
            {
              reconcileDescription.PartialPostSelections = selectionsForPartialPosting;
            }

            ReconcileResult reconcileResult = currentVersion.Reconcile(reconcileDescription);

            _alertService.Show(reconcileResult?.HasConflicts == true
              ? $"Post conflict - {reconcileResult.ToString()}"
              : "Success!");
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }


      });
    }

    public async Task ReconcileAsync()
    {
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase.GetVersionManager())
          using (Version currentVersion = versionManager.GetCurrentVersion())
          using (Version parentVersion = currentVersion.GetParent())
          {
            //ReconcileDescription and without post
            ReconcileDescription reconcileDescription = new ReconcileDescription(parentVersion)
            {
              ConflictResolutionMethod = ConflictResolutionMethod.Continue, // continue if conflicts are found
              WithPost = false
            };

            ReconcileResult reconcileResult = currentVersion.Reconcile(reconcileDescription);
            if (reconcileResult.HasConflicts)
            {
              _alertService.Show($"Reconcile conflict - {reconcileResult.ToString()}");
            }
            currentVersion.Refresh();
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });

    }


    #endregion

    #region Deleting versions
    public async Task<bool> DeleteVersionAsync(string versionNameToDelete)
    {
      bool deleteStatus = false;
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase?.GetVersionManager())
          using (Version currentVersion = versionManager?.GetCurrentVersion())
          using (Version toDeleteVersion = versionManager?.GetVersions().FirstOrDefault(version => version.GetName().Contains(versionNameToDelete)))
          {
            if (currentVersion != toDeleteVersion)
            {
              toDeleteVersion?.Delete();
              deleteStatus = true;
            }
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
      return deleteStatus;
    }



    #endregion

    #region Public helper methods

    public async Task<List<string>> GetAllDatabaseVersionsAsync()
    {
      List<string> versionNames = new List<string>();
      await QueuedTask.Run(async () =>
      {
        try
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (VersionManager versionManager = geodatabase?.GetVersionManager())
          {
            IReadOnlyList<Version> versions = versionManager?.GetVersions();

            foreach (Version version in versions ?? Enumerable.Empty<Version>())
            {
              string name = version.GetName();
              Version parent = version.GetParent();

              if (!(parent is null))
              {
                versionNames.Add(name.Split('.')[1]);
              }
            }
          }

        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
      return versionNames;
    }

    public async Task<ObservableCollection<string>> GetSelectedFeaturesForUIAsync()
    {
      ObservableCollection<string> selectedFeatures = new ObservableCollection<string>();
      await QueuedTask.Run(async () =>
      {
        try
        {
          Dictionary<FeatureLayer, Selection> selections = await GetSelectedFeaturesPerFeatureLayerAsync();
          if (selections?.Keys?.Count > 0)
          {
            foreach (KeyValuePair<FeatureLayer, Selection> selection in selections ?? Enumerable.Empty<KeyValuePair<FeatureLayer, Selection>>())
            {
              FeatureLayer featureLayer = selection.Key;
              long[] selectionObjectIds = selection.Value?.GetObjectIDs()?.ToArray();
              string[] appendFeatureLayerName = featureLayer.QueryDisplayExpressions(selectionObjectIds)?
                .Select(x => string.Concat($"{featureLayer.Name}- ", x)).ToArray();

              if (appendFeatureLayerName == null) continue;
              string displayExpressions = string.Join(Environment.NewLine, appendFeatureLayerName);
              selectedFeatures.Add(displayExpressions);
            }
          }

        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });

      return selectedFeatures;
    }

    public async Task<ObservableCollection<string>> GetAllDeletedFeaturesForUIAsync()
    {
      string deletedFeaturesLoggingTable = "L4CampsitesDeletedLog";
      ObservableCollection<string> deletedFeatures = new ObservableCollection<string>();

      await QueuedTask.Run(async () =>
      {
        using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
        using (Table table = geodatabase.OpenDataset<Table>(deletedFeaturesLoggingTable))
        using (RowCursor rowCursor = table.Search(null, false))
        {
          while (rowCursor.MoveNext())
          {
            using (Row row = rowCursor.Current)
            {
              var deletedFeatureObjectId = row["DeletedCampsiteObjectId"];
              deletedFeatures.Add($"CampsitesDeletedLog- Campsite OID: {deletedFeatureObjectId}");
            }
          }

        }
      });

      return deletedFeatures;
    }

    #endregion

    #region Private helper methods

    private async Task<ReadOnlyObservableCollection<Layer>> GetFeatureLayersFromMapAsync()
    {
      ReadOnlyObservableCollection<Layer> mapLayers = null;
      await QueuedTask.Run(() =>
      {
        try
        {
          if (MapView.Active?.Map?.Layers?.Count > 0)
          {
            mapLayers = MapView.Active.Map.Layers;
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });
      return mapLayers;
    }

    private async Task<Geodatabase> GetGeodatabaseFromActiveMapAsync()
    {
      Geodatabase geodatabase = null;
      await QueuedTask.Run(() =>
      {
        try
        {
          if (MapView.Active?.Map?.Layers?.Count > 0)
          {
            Layer layer = MapView.Active.Map.Layers[0];
            if (layer is FeatureLayer featureLayer)
            {
              geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase;
            }
          }
          else
          {
            _alertService.Show("Add layer(s) in TOC");
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
      });
      return geodatabase;
    }
    
    private async Task<Dictionary<FeatureLayer, Selection>> GetSelectedFeaturesPerFeatureLayerAsync()
    {
      Dictionary<FeatureLayer, Selection> selections = new Dictionary<FeatureLayer, Selection>();
      await QueuedTask.Run(async () =>
      {
        try
        {
          ReadOnlyObservableCollection<Layer> layers = await GetFeatureLayersFromMapAsync();
          if (layers != null)
          {
            foreach (Layer layer in layers)
            {
              if (layer is FeatureLayer featureLayer)
              {
                Selection selection = featureLayer.GetSelection();
                if (selection?.GetObjectIDs()?.Count > 0)
                {
                  selections.Add(featureLayer, selection);
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          _alertService.Show(ex.Message);
        }
      });

      return selections;
    }

    private async Task<List<Selection>> GetSelectedFeaturesForPartialPostingAsync(IEnumerable<string> selectedDeletedFeatures)
    {
      List<Selection> selections = new List<Selection>();

      await QueuedTask.Run(async () =>
     {
       // Selected features from delete table
       Selection deleteSelection = await GetSelectionFromDeletedFeaturesAsync(selectedDeletedFeatures);
       if (deleteSelection?.GetCount() > 0)
       {
         selections.Add(deleteSelection);
       }

       // Selected features from map
       Dictionary<FeatureLayer, Selection> selectionsPerFeatureLayer = await GetSelectedFeaturesPerFeatureLayerAsync();
       if (selectionsPerFeatureLayer?.Keys?.Count > 0)
       {
         foreach (KeyValuePair<FeatureLayer, Selection> selection in selectionsPerFeatureLayer)
         {
           if (selection.Value != null)
           {
             selections.Add(selection.Value);
           }
         }
       }

     });

      return selections;
    }

    private async Task<Selection> GetSelectionFromDeletedFeaturesAsync(IEnumerable<string> selectedDeletedFeatures)
    {
      Selection deleteSelection = null;
      await QueuedTask.Run( async () =>
      {
        // Deleted feature selection
        if (selectedDeletedFeatures?.Count() > 0)
        {
          using (Geodatabase geodatabase = await GetGeodatabaseFromActiveMapAsync())
          using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>("L0Campsites"))
          {
            deleteSelection = featureClass.Select(new QueryFilter(), SelectionType.ObjectID, SelectionOption.Empty);

            foreach (string deletedFeature in selectedDeletedFeatures)
            {
              long deletedFeatureObjectId = Convert.ToInt64(deletedFeature.Split(':')[1]);

              if (deletedFeatureObjectId > 0)
              {
                deleteSelection.Add(new List<long>() { deletedFeatureObjectId });
              }
            }
          }
        }
      });
      return deleteSelection;
    }

    #endregion
  }
}
