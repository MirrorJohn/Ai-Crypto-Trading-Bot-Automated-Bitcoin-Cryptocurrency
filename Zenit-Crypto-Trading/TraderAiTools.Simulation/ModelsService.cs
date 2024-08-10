using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Hallupa.Library;
using Newtonsoft.Json;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    [Flags]
    public enum ModelDataType
    {
        EMA8 = 1,
        EMA25 = 2,
        EMA50 = 4,
        Candles = 8
    }

    public class Feature
    {
        public string Label { get; set; }
        public string Market { get; set; }
        public DateTime DateTime { get; set; }
        public int LabelValue { get; set; }

        [JsonIgnore]
        public string LabelColour
        {
            get
            {
                switch (LabelValue)
                {
                    case 0:
                        return "DarkRed";
                    case 1:
                        return "Golden";

                    case 2:
                        return "Aqua";
                    case 3:
                        return "Blue";
                    case 4:
                        return "LimeGreen";
                }

                return string.Empty;
            }
        }

        public override string ToString()
        {
            return $"Label:{Label} Label value:{LabelValue} Market:{Market} Position:{DateTime:dd-MM-yy HH:mm}";
        }
    }

    public interface IModelDetails
    {
        int InputsCount { get; set; }

        ModelDataType ModelDataType { get; }

        ObservableCollection<Feature> Features { get; }

        string Name { get; set; }

        Timeframe Timeframe { get; set; }

        int TotalOutputs { get; }

        void AddFeature(Feature point);

        void RemoveFeature(Feature point);
    }

    public class ModelDetails : IModelDetails, INotifyPropertyChanged
    {
        private int _inputsCount;
        public string Name { get; set; }

        public ObservableCollection<Feature> Features { get; set; } = new ObservableCollection<Feature>();

        public void AddFeature(Feature point)
        {
            Features.Add(point);
            OnPropertyChanged("TotalOutputs");
        }

        public void RemoveFeature(Feature point)
        {
            Features.Remove(point);
            OnPropertyChanged("TotalOutputs");
        }

        public int InputsCount
        {
            get => _inputsCount;
            set
            {
                _inputsCount = value;
                OnPropertyChanged();
                OnPropertyChanged("DisplayText");
            }
        }

        //[JsonIgnore]
        // TODO public BaseModel BaseModel { get; set; }


        [JsonIgnore]
        public string DisplayText => $"{Timeframe} {Name} Inputs: {InputsCount} ModelDataType: {ModelDataType}";

        public ModelDataType ModelDataType { get; set; }

        public int TotalOutputs => Features.Select(x => x.Label).Distinct().Count();
        public Timeframe Timeframe { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [Export(typeof(ModelsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ModelsService
    {
        private IDataDirectoryService _dataDirectoryService;

        [ImportingConstructor]
        public ModelsService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;

            LoadModels();
        }

        public ObservableCollectionEx<ModelDetails> Models { get; } = new ObservableCollectionEx<ModelDetails>();

        private void LoadModels()
        {
            var saveName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.json");
            if (!File.Exists(saveName)) return;

            var models = JsonConvert.DeserializeObject<List<ModelDetails>>(File.ReadAllText(saveName));

            Models.Clear();
            Models.AddRange(models);
        }

        public void SaveModels()
        {
            var saveName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.json");
            var saveNameTemp = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.temp");
            var oldName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelDataOld.json");

            if (File.Exists(saveNameTemp)) File.Delete(saveNameTemp);

            var json = JsonConvert.SerializeObject(Models.ToList());
            File.WriteAllText(saveNameTemp, json);

            if (File.Exists(oldName)) File.Delete(oldName);
            if (File.Exists(saveName)) File.Move(saveName, oldName);

            File.Move(saveNameTemp, saveName);
        }
    }
}