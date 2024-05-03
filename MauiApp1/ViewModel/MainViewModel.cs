using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

#if ANDROID
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
#endif

namespace MauiApp1.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        readonly IConnectivity _connectivity;
        private readonly IGeolocation _geolocation;
        CancellationTokenSource cts;

        public MainViewModel(IConnectivity connectivity, IGeolocation geolocation)
        {
            Items = [];
            _connectivity = connectivity;
            _geolocation = geolocation;
        }

        public MainViewModel()
        {
            Items = [];
        }

        [ObservableProperty]
        ObservableCollection<string> items;

        [ObservableProperty]
        string text;

        [ObservableProperty]
        string cachedPosition;

        [RelayCommand]
        async Task GetCachedPosition_v1()
        {
            if(cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
            CachedPosition = "Getting cached location...";
            var loc = await _geolocation.GetLastKnownLocationAsync();
            CachedPosition = FormatLocation(loc); ;
        }

        [RelayCommand]
        async Task GetCachedPosition()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
            CachedPosition = "Getting cached location...";
#if ANDROID
            LocationManager? locMgr = Android.App.Application.Context.GetSystemService(Context.LocationService).JavaCast<LocationManager>();
            var allProviders = locMgr.AllProviders;
            var enabledProviders = locMgr.GetProviders(true);
            CachedPosition = "All providers: " + string.Join(", ", allProviders);
            CachedPosition += "\r\n" + "Enabled providers: " + string.Join(", ", enabledProviders);
            foreach (var provider in enabledProviders)
            {
                var loc = locMgr.GetLastKnownLocation(provider);
                CachedPosition += "\r\n" + provider + ": " + (loc != null ? FormatAndroidLocation(loc) : "null");
            }
#else
            var loc = await _geolocation.GetLastKnownLocationAsync();
            CachedPosition = FormatLocation(loc); ;
#endif
        }

        [RelayCommand]
        async Task GetCurrentPosition()
        {
            try
            {
                if (cts != null)
                {
                    cts.Cancel();
                    cts.Dispose();
                    cts = null;
                }
                cts = new CancellationTokenSource();
                var request = new GeolocationRequest(GeolocationAccuracy.Best);
                CachedPosition = "Getting location...";
                var location = await _geolocation.GetLocationAsync(request, cts.Token);
                CachedPosition = FormatLocation(location);
            }
            catch (Exception ex)
            {
                CachedPosition = FormatLocation(null, ex);
            }
            finally
            {
                cts.Dispose();
                cts = null;
            }
        }

        [RelayCommand]
        async Task ListenForPosition()
        {
            Console.WriteLine("Add polling listener for 60 seconds... (console)");

#if ANDROID
            try
            {
                CachedPosition = "Add polling listener for 60 seconds...";
                var startTime = DateTime.Now;

                // LocationManager.FusedProvider is only supported on: 'android' 31.0 or later 
                var providers = new string[] { LocationManager.PassiveProvider, LocationManager.FusedProvider, LocationManager.GpsProvider, LocationManager.NetworkProvider };

                var posCount = new Dictionary<string, int>();
                var lastElapsedTime = new Dictionary<string, long>();
                foreach (var provider in providers)
                {
                    posCount[provider] = 0;
                    lastElapsedTime[provider] = 0;
                }

                var listenSeconds = 60;
                var elapsedSeconds = 0;
                while ((elapsedSeconds = (int)(DateTime.Now - startTime).TotalSeconds) <= listenSeconds)
                {
                    var remainingTime = listenSeconds - elapsedSeconds;
                    CachedPosition = remainingTime > 0 ? "Listener remaining time: " + (listenSeconds - elapsedSeconds) : "";
                    var locMgr = Android.App.Application.Context.GetSystemService(Context.LocationService).JavaCast<LocationManager>();
                    foreach (var provider in providers)
                    {
                        var loc = locMgr.GetLastKnownLocation(provider);
                        // Location.ElapsedRealtimeMillis is only supported on: 'android' 33.0 or later
                        if ((loc != null) && lastElapsedTime[provider] != loc.ElapsedRealtimeMillis)
                        {
                            lastElapsedTime[provider] = loc.ElapsedRealtimeMillis;
                            posCount[provider]++;
                        }
                        CachedPosition += "\r\n" + provider + ": " + posCount[provider] + " " + (loc != null ? FormatAndroidLocation(loc) : "null");
                    }
                    await Task.Delay(5);
                }
                CachedPosition += "\r\n\r\nPolling listener finished after " + listenSeconds + " seconds";
                foreach(var provider in providers)
                {
                    CachedPosition += "\r\nPosition update rate: " + Math.Round(posCount[provider] / (double)listenSeconds, 2) + "Hz";
                }
            }
            catch (Exception ex)
            {
                CachedPosition = "Could not add listener: " + ex.Message;
            }
#endif

        }

        [RelayCommand]
        async Task SetMockLocation()
        {
            // https://stackoverflow.com/questions/38251741/how-to-set-android-mock-gps-location

            try
            {
                Text = "Try to set mock location...";

#if ANDROID
                // https://www.youtube.com/watch?v=JCgxK0pWjNg
                LocationManager? locMgr = Android.App.Application.Context.GetSystemService(Context.LocationService).JavaCast<LocationManager>();
                if (locMgr == null)
                {
                    Text = "Location manager is null!";
                    return;
                }

                // Run for 60 seconds
                for (var i = 0; i < 60; i++)
                {
                    await Task.Delay(1000);  // Simulate 1Hz update rate

                    Text = locMgr.IsLocationEnabled ? "Location enabled" : "Location disabled";

                    // To be able to set mock location, we need to
                    // Step 1: Give a permission in Android Manifest
                    // <uses-permission android: name = "android.permission.ACCESS_MOCK_LOCATION" />
                    // Step 2: In your real device,
                    // Go to Setting --> Developer option-- > Mock location app option and select your app for testing.

                    var enabledProviders = locMgr.GetProviders(true).Where(x => x == LocationManager.FusedProvider || x == LocationManager.GpsProvider || x == LocationManager.NetworkProvider);

                    Text += "\r\n Providers to mock: " + string.Join(", ", enabledProviders);
                    Text += "\r\n Iteration " + i;
                    foreach (var locationProvider in enabledProviders)
                    {
                        locMgr.AddTestProvider(
                            locationProvider, 
                            requiresNetwork: false,
                            requiresSatellite: false,
                            requiresCell: false,
                            hasMonetaryCost: false,
                            supportsAltitude: true,
                            supportsSpeed: true,
                            supportsBearing: true,
                            powerRequirement: Power.Low,
                            accuracy: Android.Hardware.SensorStatus.AccuracyMedium);

                        // https://learn.microsoft.com/en-us/dotnet/api/android.locations.location?view=net-android-34.0
                        // https://developer.android.com/reference/android/location/Location
                        var mockLoc = new Android.Locations.Location(locationProvider);
                        mockLoc.Latitude = 58.7338632;
                        mockLoc.Longitude = 5.6515026;
                        mockLoc.Altitude = 66.25;
                        mockLoc.Accuracy = 0.012f;

                        mockLoc.Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        mockLoc.ElapsedRealtimeNanos = SystemClock.ElapsedRealtimeNanos();
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                        {
                            mockLoc.VerticalAccuracyMeters = 0.234f;
                            mockLoc.SpeedAccuracyMetersPerSecond = 0.6f;
                            mockLoc.BearingAccuracyDegrees = 0.7f;
                        }

                        locMgr.SetTestProviderEnabled(locationProvider, true);
                        locMgr.SetTestProviderLocation(locationProvider, mockLoc);
                        Text += $"\r\n LocationProvider {locationProvider} updated!";
                    }
                }

#endif
            }
            catch (Exception ex)
            {
                Text = FormatLocation(null, ex);
            }
        }

        private string FormatLocation(Microsoft.Maui.Devices.Sensors.Location? loc, Exception ex = null)
        {
            if (loc == null)
            {
                return $"Unable to detect location. Exception: {ex?.Message ?? string.Empty}";
            }
            return $"Lat: {loc.Latitude.ToString(CultureInfo.InvariantCulture)}, " +
            $"Lon: {loc.Longitude.ToString(CultureInfo.InvariantCulture)}, " +
            $"Acc: {ToStr(loc.Accuracy)}, " +

            $"Altitude: {ToStr(loc.Altitude)}, " +
            $"VAcc: {ToStr(loc.VerticalAccuracy)}, " +
            
            $"Time: {loc.Timestamp.ToLocalTime().ToString("hh:mm:ss")}, " +
            $"Mock: {loc.IsFromMockProvider}";
        }

#if ANDROID
        private string FormatAndroidLocation(Android.Locations.Location? loc)
        {
            return 
                $"Lat: {loc.Latitude.ToString(CultureInfo.InvariantCulture)}, " +
                $"Lon: {loc.Longitude.ToString(CultureInfo.InvariantCulture)}, " +
                $"Acc: {ToStr(loc.Accuracy)}, " +

                $"Altitude: {ToStr(loc.Altitude)}, " +
                $"VAcc: {(loc.HasVerticalAccuracy ? ToStr(loc.VerticalAccuracyMeters) : "")}, " +

                $"Time: {DateTime.UnixEpoch.AddMilliseconds(loc.Time).ToLocalTime().ToString("hh:mm:ss")}, " +
                $"Mock: {loc.IsFromMockProvider}";
        }
#endif

        private string ToStr(double? accuracy)
        {
            return (accuracy ?? 0).ToString("0.00", CultureInfo.InvariantCulture);
        }

    }
}
