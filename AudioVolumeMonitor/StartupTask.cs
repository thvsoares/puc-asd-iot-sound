using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using System.Runtime.InteropServices;
using Windows.Media;
using Windows.Foundation;
using Windows.Media.Audio;
using System.Diagnostics;
using System.Threading.Tasks;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace AudioVolumeMonitor
{
    public sealed class StartupTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //
            var deferral = taskInstance.GetDeferral();

            await InitAudioGraph();

            deferral.Complete();
        }

        AudioGraph audioGraph;
        AudioDeviceInputNode deviceInputNode;
        AudioFrameOutputNode frameOutputNode;

        private async Task InitAudioGraph()
        {
            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                Debug.WriteLine("AudioGraph creation error: " + result.Status.ToString());
            }
            audioGraph = result.Graph;
            CreateAudioDeviceInputNodeResult result1 = await audioGraph.CreateDeviceInputNodeAsync(Windows.Media.Capture.MediaCategory.Media);

            if (result1.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output node
                Debug.WriteLine(result.Status.ToString());
            }
            deviceInputNode = result1.DeviceInputNode;
            frameOutputNode = audioGraph.CreateFrameOutputNode();
            deviceInputNode.AddOutgoingConnection(frameOutputNode);
            audioGraph.Start();
            audioGraph.QuantumProcessed += AudioGraph_QuantumProcessed;
        }
        private void AudioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            Debug.WriteLine("event called");
            AudioFrame frame = frameOutputNode.GetFrame();
            ProcessFrameOutput(frame);
        }
        unsafe private void ProcessFrameOutput(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;
                float* dataInFloat;

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                dataInFloat = (float*)dataInBytes;

                for (int i = 0; i < audioGraph.SamplesPerQuantum; i++)
                    Debug.WriteLine(dataInFloat[i]);
            }
        }

        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        unsafe interface IMemoryBufferByteAccess
        {
            void GetBuffer(out byte* buffer, out uint capacity);
        }
    }
}
