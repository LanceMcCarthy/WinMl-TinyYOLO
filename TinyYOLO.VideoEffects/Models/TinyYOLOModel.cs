using System;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Foundation;
using Windows.Storage;

namespace TinyYOLO.VideoEffects.Models
{
    public sealed class TinyYOLOModel
    {
        private LearningModelPreview learningModel;

        public static IAsyncOperation<TinyYOLOModel> CreateTinyYoloModelOperation(StorageFile file)
        {
            return CreateTinyYOLOModel(file).AsAsyncOperation();
        }

        internal static async Task<TinyYOLOModel> CreateTinyYOLOModel(StorageFile file)
        {
            LearningModelPreview learningModel = await LearningModelPreview.LoadModelFromStorageFileAsync(file);
            TinyYOLOModel model = new TinyYOLOModel();
            model.learningModel = learningModel;
            return model;
        }

        public IAsyncOperation<TinyYOLOModelOutput> EvaluateOperationAsync(TinyYOLOModelInput input)
        {
            return EvaluateAsync(input).AsAsyncOperation();
        }

        internal async Task<TinyYOLOModelOutput> EvaluateAsync(TinyYOLOModelInput input)
        {
            TinyYOLOModelOutput output = new TinyYOLOModelOutput();
            LearningModelBindingPreview binding = new LearningModelBindingPreview(learningModel);
            binding.Bind("image", input.image);
            binding.Bind("grid", output.grid);
            LearningModelEvaluationResultPreview evalResult = await learningModel.EvaluateAsync(binding, string.Empty);
            return output;
        }
    }
}
