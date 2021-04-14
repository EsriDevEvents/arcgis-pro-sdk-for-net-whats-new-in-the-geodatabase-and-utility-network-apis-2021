using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ActiproSoftware.Windows.Extensions;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using GalaSoft.MvvmLight.Command;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;
using RelayCommand = GalaSoft.MvvmLight.Command.RelayCommand;

namespace VersionManagementDemo
{
  internal class VersionOperationDockPaneViewModel : DockPane
  {

    #region Private members

    private const string DockPaneId = "VersionManagementDemo_VersionOperationDockPane";
    private readonly VersionOperationsService _versionOperationsService;
    private IEnumerable<string> _selectedDeletedFeatures;

    #endregion

    #region Properties

    private ObservableCollection<string> _versions;
    public ObservableCollection<string> Versions
    {
      get => _versions;
      set
      {
        _versions = value;
        SetProperty(ref _versions, value, () => Versions);
        NotifyPropertyChanged(nameof(Versions));
      }
    }

    private ObservableCollection<string> _deletedFeatures;
    /// <summary>
    /// List of deleted features from 'delete table'
    /// </summary>
    public ObservableCollection<string> DeletedFeatures
    {
      get => _deletedFeatures;
      set
      {
        SetProperty(ref _deletedFeatures, value, () => DeletedFeatures);
      }
    }
    
    private ObservableCollection<string> _selectedFeatures;
    /// <summary>
    /// List of the current active map's identified features
    /// </summary>
    public ObservableCollection<string> SelectedFeatures
    {
      get => _selectedFeatures; 
      set
      {
        SetProperty(ref _selectedFeatures, value, () => SelectedFeatures);

      }
    }

    private string _selectedVersion;
    public string SelectedVersion
    {
      get => _selectedVersion;
      set
      {
        _selectedVersion = value;
        SetProperty(ref _selectedVersion, value, () => _selectedVersion);
        NotifyPropertyChanged(nameof(SelectedVersion));
      }
    }

    private string _versionName;
    public string VersionName
    {
      get => _versionName;
      set
      {
        _versionName = value;
        SetProperty(ref _versionName, value, () => VersionName);
      }
    }

    private bool _isVersionSelectionActive = true;
    public bool IsVersionSelectionActive
    {
      get => _isVersionSelectionActive;
      set
      {
        _isVersionSelectionActive = value;
        SetProperty(ref value, _isVersionSelectionActive, () => IsVersionSelectionActive);
        NotifyPropertyChanged(nameof(IsVersionSelectionActive));
      }
    }

    private bool _isVersionSelected = false;
    public bool IsVersionSelected
    {
      get => _isVersionSelected;
      set
      {
        _isVersionSelected = value;
        SetProperty(ref value, _isVersionSelected, () => IsVersionSelected);
        NotifyPropertyChanged(nameof(IsVersionSelected));
      }
    }

    /// <summary>
    /// Text shown near the top of the DockPane.
    /// </summary>
    private string _heading = "Versioning Variations Demo 2021";
    public string Heading
    {
      get { return _heading; }
      set
      {
        SetProperty(ref _heading, value, () => Heading);
      }
    }
    
    #endregion

    #region RelayCommands

    private readonly RelayCommand _createVersionCommand;
    private readonly RelayCommand _resetVersionCommand;
    private readonly RelayCommand<object> _deleteVersionCommand;
    private readonly RelayCommand<object> _selectVersionCommand;
    private readonly RelayCommand _postCommand;
    private readonly RelayCommand _reconcileCommand;
    private readonly RelayCommand _addSelectedFeaturesForPartialPostCommand;
    private readonly RelayCommand _clearPartialPostFeatureCommand;
    private readonly RelayCommand<object> _deletedFeaturesSelectionChangedCommand;
    private readonly RelayCommand _showDeletedFeaturesCommand;

    public RelayCommand CreateVersionCommand => _createVersionCommand;
    public RelayCommand ResetVersionCommand => _resetVersionCommand;
    public RelayCommand<object> DeleteVersionCommand => _deleteVersionCommand;
    public RelayCommand<object> SelectVersionCommand => _selectVersionCommand;
    public RelayCommand PostCommand => _postCommand;
    public RelayCommand ReconcileCommand => _reconcileCommand;
    public RelayCommand ClearPartialPostFeatureCommand => _clearPartialPostFeatureCommand;
    public RelayCommand AddSelectedFeaturesForPartialPostCommand => _addSelectedFeaturesForPartialPostCommand;
    public RelayCommand<object> DeletedFeaturesSelectionChangedCommand => _deletedFeaturesSelectionChangedCommand;
    public RelayCommand ShowDeletedFeaturesCommand => _showDeletedFeaturesCommand;

    #endregion

    #region Constructor

    protected VersionOperationDockPaneViewModel()
    {
      _versions = new ObservableCollection<string>();
      _selectedFeatures = new ObservableCollection<string>();
      _createVersionCommand = new RelayCommand(CreateVersionAsync);
      _resetVersionCommand = new RelayCommand(ResetVersionToDefaultAsync);
      _deleteVersionCommand = new RelayCommand<object>((args) => DeleteVersionAsync(args));
      _selectVersionCommand = new RelayCommand<object>((args) =>SelectVersionAsync(args));
      _postCommand = new RelayCommand(PostOperationAsync);
      _reconcileCommand = new RelayCommand(ReconcileOperationAsync);
      _clearPartialPostFeatureCommand = new RelayCommand(ClearPartialPostFeatures);
      _addSelectedFeaturesForPartialPostCommand = new RelayCommand(AddSelectedFeaturesForPartialPostAsync);
      _deletedFeaturesSelectionChangedCommand = new RelayCommand<object>(args => DeletedFeaturesSelectionChanged(args));
      _showDeletedFeaturesCommand = new RelayCommand(ShowDeletedFeaturesAsync);
      _versionOperationsService = new VersionOperationsService(new AlertService());
      
      Module1.VersionOperationDockPaneVM = this;
    }

    #endregion

    #region Creating  and delete versions

    private async void CreateVersionAsync()
    {
      string versionName = await _versionOperationsService.CreateVersionAsync(_versionName);
      if (!string.IsNullOrWhiteSpace(versionName))
      {
        Versions.Add(_versionName);
      }
      else
      {
        Versions.AddRange(await _versionOperationsService.GetAllDatabaseVersionsAsync());
      }
    }

    private async void DeleteVersionAsync(object versionName)
    {
      bool isVersionDeleted = await _versionOperationsService.DeleteVersionAsync(versionName.ToString());
      if (isVersionDeleted)
      {
        Versions.Remove(versionName.ToString());
      }
    }

    #endregion

    #region Choose versions
    private async void ResetVersionToDefaultAsync()
    {
      await _versionOperationsService.ResetToDefaultVersionAsync();
      Versions.Clear();
      Versions.AddRange(await _versionOperationsService.GetAllDatabaseVersionsAsync());
      IsVersionSelected = false;
      ClearSelectedFeatures();
    }
    
    private async void SelectVersionAsync(object versionSelected)
    {
      var index = Versions.IndexOf(versionSelected.ToString());
      
      SelectedVersion = versionSelected.ToString();
      IsVersionSelected = true;
      
      await _versionOperationsService.ChangeVersionAsync(versionSelected.ToString());
    }
    #endregion

    #region Reconcile and Post
    private async void ReconcileOperationAsync()
    {
      await _versionOperationsService.ReconcileAsync();
    }

    private async void PostOperationAsync()
    {
      await _versionOperationsService.PostAsync(_selectedDeletedFeatures);
    }
    #endregion
    
    #region Helper methods

    private async void ShowDeletedFeaturesAsync()
    {
      DeletedFeatures = await _versionOperationsService.GetAllDeletedFeaturesForUIAsync();

    }
    
    private async void AddSelectedFeaturesForPartialPostAsync()
    {
      SelectedFeatures =  await _versionOperationsService.GetSelectedFeaturesForUIAsync();
    }

    private void ClearSelectedFeatures()
    {
      ProApp.Current.Dispatcher.BeginInvoke(new Action(() =>
      {
        SelectedFeatures.Clear();
      }));
    }
   
    private void ClearPartialPostFeatures()
    {
      ClearSelectedFeatures();
    }
    
    private void DeletedFeaturesSelectionChanged(object selectedItems)
    {
      System.Collections.IList items = selectedItems as System.Collections.IList;
      _selectedDeletedFeatures = items?.Cast<string>();
    }
    
    #endregion

    /// <summary>
    /// Show the DockPane.
    /// </summary>
    internal static void Show()
    {
      DockPane pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
      if (pane == null)
        return;

      pane.Activate();
    }
  }


  /// <summary>
  /// Button implementation to show the DockPane.
  /// </summary>
	internal class VersionOperationDockPane_ShowButton : Button
  {
    protected override void OnClick()
    {
      VersionOperationDockPaneViewModel.Show();
    }
  }













}
