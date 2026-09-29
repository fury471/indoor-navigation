import RoomPlan

// Project-specific wrapper around Apple's capability check.
// https://developer.apple.com/documentation/roomplan/roomcapturesession/issupported
//
// Return contract:
//  1 = supported
//  0 = device not supported
// -1 = iOS version too old

@_cdecl("INRoomPlan_GetSupport")
public func INRoomPlan_GetSupport() -> Int32 {
    if #available(iOS 16.0, *) {
        return RoomCaptureSession.isSupported ? 1 : 0
    }

    return -1
}