// The barometer, as Core Motion's relative altitude (build 21): tells the app when the user has
// changed floor by lift or stairs. Apple: CMAltimeter (Core Motion). The app's Info.plist needs
// NSMotionUsageDescription, or the app stops when this starts (ConfigureMotionUsage.cs adds it).
// Pattern: Unity documentation, "Create a native plug-in for iOS" (extern "C" functions for DllImport).

#import <CoreMotion/CoreMotion.h>

static CMAltimeter *inAltimeter = nil;
static double inMetres = 0.0;
static double inSeconds = -1.0;
static int inState = 0; // 0 stopped, 1 running, -1 no barometer, -2 not allowed or failed

extern "C"
{
    int INAltimeter_IsAvailable()
    {
        return [CMAltimeter isRelativeAltitudeAvailable] ? 1 : 0;
    }

    void INAltimeter_Start()
    {
        if (inAltimeter != nil) return;
        if (![CMAltimeter isRelativeAltitudeAvailable])
        {
            inState = -1;
            return;
        }

        inAltimeter = [[CMAltimeter alloc] init];
        inState = 1;
        [inAltimeter startRelativeAltitudeUpdatesToQueue:[NSOperationQueue mainQueue]
                                             withHandler:^(CMAltitudeData *data, NSError *error) {
            if (error != nil || data == nil)
            {
                inState = -2;
                return;
            }

            inMetres = data.relativeAltitude.doubleValue;
            inSeconds = data.timestamp;
            inState = 1;
        }];
    }

    void INAltimeter_Stop()
    {
        if (inAltimeter != nil) [inAltimeter stopRelativeAltitudeUpdates];
        inAltimeter = nil;
        inMetres = 0.0;
        inSeconds = -1.0;
        inState = 0;
    }

    // 1 with a reading; otherwise the state (0 stopped or waiting, -1 no barometer, -2 not allowed).
    int INAltimeter_Read(double *metres, double *seconds)
    {
        *metres = inMetres;
        *seconds = inSeconds;
        if (inState == 1 && inSeconds >= 0) return 1;
        return inState == 1 ? 0 : inState;
    }
}
