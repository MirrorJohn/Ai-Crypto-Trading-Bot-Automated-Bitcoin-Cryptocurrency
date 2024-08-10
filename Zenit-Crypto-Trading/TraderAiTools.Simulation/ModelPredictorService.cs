using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hallupa.Library;
//using Keras.Models;
using log4net;
using Numpy;
using Numpy.Models;
using Python.Runtime;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    [Export(typeof(ModelPredictorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ModelPredictorService
    {
        private readonly ModelsService _modelsService;
        private readonly IDataDirectoryService _dataDirectoryService;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Py.GILState _y;
        private ProducerConsumer<Action> _producerConsumer;
        private object _lock = new object();

        [ImportingConstructor]
        public ModelPredictorService(ModelsService modelsService, IDataDirectoryService dataDirectoryService)
        {
            _modelsService = modelsService;
            _dataDirectoryService = dataDirectoryService;
            //_producerConsumer = new ProducerConsumer<Action>(1, ConsumeAction);
            //_producerConsumer.Add(() => _y = Py.GIL());
            //_producerConsumer.Start();
        }

        private ProducerConsumerActionResult ConsumeAction(Action arg)
        {
            try
            {
                arg();
            }
            catch (Exception ex)
            {
                Log.Error("Error processing", ex);
            }

            return ProducerConsumerActionResult.Success;
        }

        public int Predict(IModelDetails modelDetails, params float[] xValues)
        {
            var yValue = -1;
            ManualResetEvent wait = new ManualResetEvent(false);
            _producerConsumer.Add(() =>
            {
                /*var x = np.array(np.array(xValues)).reshape(new Shape(1, xValues.Length));

                var y = ((ModelDetails)modelDetails).BaseModel.Predict(x)[0];

                // Get which index is highest
                for (var i = 0; i < y.size; i++)
                {
                    if ((float)y[i] > 0.9 && (yValue == -1 || (float)y[i] > (float)y[yValue])) yValue = i;
                }*/

                wait.Set();
            });

            wait.WaitOne();
            return yValue;
        }

        public IModelDetails LoadModel(string name)
        {
            var model = _modelsService.Models.First(x => x.Name == name);
            /* TODO if (model.BaseModel != null) return model;

            lock (_lock)
            {
                BaseModel baseModel = null;
                var waitEvent = new ManualResetEvent(false);
                _producerConsumer.Add(() =>
                {
                    var path = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "Models", model.Name, "model.h5");
                    Log.Info($"Loading model: {model.Name}");
                    baseModel = BaseModel.LoadModel(path);
                    Log.Info("Model loaded");
                    waitEvent.Set();
                });

                waitEvent.WaitOne();

                model.BaseModel = baseModel;
                return model;
            }*/

            return null;
        }
    }
}