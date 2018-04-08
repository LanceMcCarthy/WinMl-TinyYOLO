using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using TinyYOLO.VideoEffects.Helpers;
using TinyYOLO.VideoEffects.Models;

namespace TinyYOLO.VideoEffects
{
    public sealed class YoloVideoEffectDefinition : IBasicVideoEffect
    {
        // ** Fields ** //

        // Video Effect Fields
        private VideoEncodingProperties currentEncodingProperties;
        private CanvasDevice canvasDevice;
        private IPropertySet currentConfiguration;

        // WinML Fields
        private LearningModelPreview model;
        private ImageVariableDescriptorPreview inputImageDescription;
        private TensorVariableDescriptorPreview outputTensorDescription;
        private YoloWinMlParser parser;

        // General
        private bool isLoadingModel;
        private int frameCount = 0;
        
        // ** Properties ** //

        /// <summary>
        /// The path for the model file
        /// </summary>
        public Uri ModelUri { get; set; } = new Uri("ms-appx:///Assets/TinyYOLO.onnx");

        /// <summary>
        /// Reports status of model loading and effect processing
        /// </summary>
        public string Status { get; set; }


        // ** Methods ** //

        // This is run for every video frame passed in the media pipleine (MediaPlayer, MediaCapture, etc)
        public async void ProcessFrame(ProcessVideoFrameContext context)
        {
            frameCount = frameCount + 1;

            if(frameCount % 5 != 0)
                return;

            Debug.WriteLine($"ProcessFrame hit - FrameCount: {frameCount}");

            // wait for model to complete load
            if (isLoadingModel)
            {
                Debug.WriteLine($"isLoadingModel = {isLoadingModel}... returning from ProcessFrame early");
                return;
            }

            // Load the model, skips if already loaded.
            await LoadModelAsync();

            // Evaluate
            var boundingBoxes = await EvaluateVideoFrameAsync(context.InputFrame);

            if (boundingBoxes.Count <= 0)
            {
                Debug.WriteLine($"No Bounding Boxes Discovered... returning from ProcessFrame early.");
                return;
            }

            // Remove overalapping and low confidence bounding boxes
            var filteredBoxes = parser.NonMaxSuppress(boundingBoxes, 5, .5F);

            if (filteredBoxes.Count <= 0)
            {
                Debug.WriteLine($"No filteredBoxes Discovered... returning from ProcessFrame early.");
                return;
            }
            else
            {
                Debug.WriteLine($"{filteredBoxes.Count} Bounding Boxes remain after removing low confidence and overlapping boxes");
            }

            // ********** Draw Boxes directly on VideoFrame ********** //

            // IMPORTANT - InputFrame.SoftwareBitmap if using CPU memory
            if (context.InputFrame.Direct3DSurface == null)
            {
                // InputFrame's raw pixels
                byte[] inputFrameBytes = new byte[4 * context.InputFrame.SoftwareBitmap.PixelWidth * context.InputFrame.SoftwareBitmap.PixelHeight];
                context.InputFrame.SoftwareBitmap.CopyToBuffer(inputFrameBytes.AsBuffer());

                using (var inputBitmap = CanvasBitmap.CreateFromBytes(canvasDevice, inputFrameBytes, context.InputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight,context.InputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat()))
                using (var renderTarget = new CanvasRenderTarget(canvasDevice, context.OutputFrame.SoftwareBitmap.PixelWidth, context.OutputFrame.SoftwareBitmap.PixelHeight, (float) context.OutputFrame.SoftwareBitmap.DpiX, context.OutputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat(), CanvasAlphaMode.Premultiplied))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelWidth - x, box.Width);
                        var h = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelHeight - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }

                return;
            }

            // TODO GPU Support
            // IMPORTANT - InputFrame.Direct3DSurface if using CPU memory
            if (context.InputFrame.SoftwareBitmap == null)
            {
                using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
                using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelWidth - x, box.Width);
                        var h = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelHeight - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }
            }
        }

        // Loads the ML model file and sets 
        private async Task LoadModelAsync()
        {
            if (model != null)
                return;

            Debug.WriteLine($"Loading Model");

            isLoadingModel = true;

            try
            {
                // Load Model
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(ModelUri);
                Debug.WriteLine($"Model file discovered at: {modelFile.Path}");

                model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);
                Debug.WriteLine($"LearningModelPReview object instantiated: {modelFile.Path}");

                // Retrieve model input and output variable descriptions (we already know the model takes an image in and outputs a tensor)
                var inputFeatures = model.Description.InputFeatures.ToList();
                Debug.WriteLine($"{inputFeatures.Count} Input Features");

                var outputFeatures = model.Description.OutputFeatures.ToList();
                Debug.WriteLine($"{inputFeatures.Count} Output Features");

                inputImageDescription = inputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Image)
                    as ImageVariableDescriptorPreview;

                outputTensorDescription = outputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Tensor)
                    as TensorVariableDescriptorPreview;
            }
            catch (Exception ex)
            {
                Status = $"Error loading model: {ex.Message}";
                Debug.WriteLine($"Error loading model: {ex.Message}");
                model = null;
            }
            finally
            {
                isLoadingModel = false;
            }
        }


        private async Task<IList<YoloBoundingBox>> EvaluateVideoFrameAsync(VideoFrame inputFrame)
        {
            if (inputFrame == null)
                return null;

            try
            {
                // Create bindings for the input and output buffer
                var binding = new LearningModelBindingPreview(model);

                // R4 WinML does needs the output pre-allocated for multi-dimensional tensors
                var outputArray = new List<float>();
                outputArray.AddRange(new float[21125]);  // Total size of TinyYOLO output

                binding.Bind(inputImageDescription.Name, inputFrame);
                binding.Bind(outputTensorDescription.Name, outputArray);

                // Process the frame with the model
                var results = await model.EvaluateAsync(binding, "TinyYOLO");
                var resultProbabilities = results.Outputs[outputTensorDescription.Name] as List<float>;

                // Use out helper to parse to the YOLO outputs into bounding boxes with labels
                var boxes = parser.ParseOutputs(resultProbabilities?.ToArray(), .3F);

                Status = $"Model Evaluation Completed. Boxes: {boxes.Count}";
                Debug.WriteLine($"Model Evaluation Completed. Bounding Boxes: {boxes.Count}");

                return boxes;
            }
            catch (Exception ex)
            {
                Status = $"error: {ex.Message}";
                return null;
            }
        }
        
        // ********** IBasicVideoEffect ********** //

        public void SetProperties(IPropertySet configuration)
        {
            this.currentConfiguration = configuration;
        }

        public async void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            currentEncodingProperties = encodingProperties;

            canvasDevice = device != null ? CanvasDevice.CreateFromDirect3D11Device(device) : CanvasDevice.GetSharedDevice();

            parser = new YoloWinMlParser();

            await LoadModelAsync();
        }

        public void Close(MediaEffectClosedReason reason)
        {
            canvasDevice?.Dispose();
        }
        
        public MediaMemoryTypes SupportedMemoryTypes => EffectConstants.SupportedMemoryTypes;

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties => EffectConstants.SupportedEncodingProperties;
        
        public bool IsReadOnly => false;
        public bool TimeIndependent => false;
        public void DiscardQueuedFrames() { }
    }
}