public interface IVideoCapture : IDisposable
{
	VideoFrame CaptureFrame();
}
