namespace URPSceneDoctor.Editor
{
    public static class USD_RenderScanner
    {
        public static USD_ScanSnapshot CaptureSnapshot()
        {
            // Render doctor shares the same snapshot source for v0.1 to avoid duplicate scans.
            return USD_AtmosScanner.CaptureSnapshot();
        }
    }
}
