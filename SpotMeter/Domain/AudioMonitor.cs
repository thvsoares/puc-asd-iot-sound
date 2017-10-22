using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace SpotMeter.Domain
{
    /// <summary>
    /// Processes the input from the default audio input device
    /// </summary>
    public class AudioMonitor : IDisposable
    {
        /// <summary>
        /// Used to configure the graph with the default input data been sent to the frame output node
        /// </summary>
        private AudioGraph _graph;

        /// <summary>
        /// Represents de default audio input device (hopefully a microphone :P)
        /// </summary>
        private AudioDeviceInputNode _deviceInputNode;

        /// <summary>
        /// Represents the audio frame processer node
        /// </summary>
        private AudioFrameOutputNode _audioFrameOutputNode;

        /// <summary>
        /// Sum of the last averages until a second is computed
        /// </summary>
        private double _cumulativeAverage;

        /// <summary>
        /// Sum of milliseconds until a second is computed
        /// </summary>
        private double _cumulativeMilliseconds;

        /// <summary>
        /// Number of quantum events processed until a second is computed
        /// </summary>
        private int _cumulativeReads;

        /// <summary>
        /// The average of the last second cumulative data
        /// </summary>
        private double _lastSecondAverage;

        /// <summary>
        /// Raised when something happens in the class
        /// </summary>
        public event Action<string> OnNotify;

        /// <summary>
        /// Raised when the average sound level reading of the last second change
        /// </summary>
        public event Action<double> OnAverageLevelChanged;

        /// <summary>
        /// Initialize the graph and starts the processing events
        /// </summary>
        /// <returns>True if everything goes well</returns>
        public async Task<bool> Start()
        {
            // Audio graph settings
            var graphSettings = new AudioGraphSettings(AudioRenderCategory.Other);

            // Input an Output device encoding properties
            AudioEncodingProperties deviceEncodingProperties = AudioEncodingProperties.CreatePcm(sampleRate: 44100, channelCount: 1, bitsPerSample: 32);
            deviceEncodingProperties.Subtype = MediaEncodingSubtypes.Float;

            // Create a new audio graph
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(graphSettings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                OnNotify?.Invoke($"AudioGraph creation error {result.Status}");
                return false;
            }
            _graph = result.Graph;

            // Create a device input node using the default audio input device
            CreateAudioDeviceInputNodeResult deviceInputNodeResult = await _graph.CreateDeviceInputNodeAsync(MediaCategory.Other, deviceEncodingProperties);
            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                OnNotify?.Invoke($"Input device configuration error {result.Status}");
                return false;
            }
            _deviceInputNode = deviceInputNodeResult.DeviceInputNode;

            // Creates the Output processing node
            _audioFrameOutputNode = _graph.CreateFrameOutputNode(deviceEncodingProperties);
            // Configure the path between the input device and the output node
            _deviceInputNode.AddOutgoingConnection(_audioFrameOutputNode);
            // Wire the quantum processed event
            _graph.QuantumProcessed += Graph_QuantumProcessed;

            // Initialize the control variables to ensure an average based on one second of data
            ResetCumulativeVariables();
            _lastSecondAverage = 0;

            OnNotify?.Invoke("Audio monitoring initiated");

            return true;
        }

        /// <summary>
        /// Stop and destroy the graph
        /// </summary>
        public void Stop()
        {
            if (_graph != null)
            {
                _graph.Stop();
                _graph.Dispose();
                _graph = null;
            }
        }

        /// <summary>
        /// Handles a quantum processed event to get the audio frame
        /// </summary>
        /// <param name="sender">The configured audio graph</param>
        /// <param name="args"></param>
        private void Graph_QuantumProcessed(AudioGraph sender, object args)
        {
            AudioFrame frame = _audioFrameOutputNode.GetFrame();
            ProcessFrameOutput(frame);
        }

        /// <summary>
        /// Processes the data of the current frame and the last second data
        /// </summary>
        /// <param name="frame">Frame to be processed</param>
        private unsafe void ProcessFrameOutput(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                float* dataInFloat;
                uint capacityInBytes, dataInLenght;
                float avg = 0;
                double? secondAverage = null;

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                // Pointer to the raw data in memory enconded in floats
                dataInFloat = (float*)dataInBytes;
                // Compute the samples count
                dataInLenght = capacityInBytes / sizeof(float);

                // Compute the average of this audio frame
                for (int i = 0; i < dataInLenght; i++)
                    avg += Math.Abs(dataInFloat[i]);

                // Update shared variables in a lock to avoid race condition
                lock (this)
                {
                    // Increment the cumulative variables
                    _cumulativeAverage += avg;
                    _cumulativeMilliseconds += frame.Duration?.TotalMilliseconds ?? 0;
                    _cumulativeReads++;

                    // Compute the second average when enough data is added
                    if (_cumulativeMilliseconds >= 1000)
                    {
                        secondAverage = _cumulativeAverage / _cumulativeReads;
                        ResetCumulativeVariables();
                    }
                }

                //Notify the volume change outside of the locked context to minimize any delay impact
                if (secondAverage.HasValue && secondAverage.Value != _lastSecondAverage)
                {
                    OnAverageLevelChanged?.Invoke(secondAverage.Value);
                    _lastSecondAverage = secondAverage.Value;
                }
#if DEBUG
                if (dataInLenght > 0)
                {
                    avg = avg / dataInLenght;
                    Debug.WriteLine("avg [{0}],spl [{1}], dur [{1}]", avg, dataInLenght, frame.Duration?.TotalMilliseconds);
                }
#endif
            }
        }

        private void ResetCumulativeVariables()
        {
            _cumulativeAverage = 0;
            _cumulativeMilliseconds = 0;
            _cumulativeReads = 0;
        }

        public void Dispose()
        {
            if (_graph != null)
                _graph.Dispose();
        }
    }
}
