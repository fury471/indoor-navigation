# First building localisation experiment

## Goal
After restarting the app, recover the phone's pose relative to
a prepared room and display a marker at a known physical location.

## Scope
One room and its doorway within the intended two-floor demo site.

## Proposed capture
RoomPlan for simplified architectural geometry.

## Localisation
Method to be selected and validated.
A RoomPlan geometry export alone is not sufficient evidence of localisation.

## Test procedure
1. Capture the room and the required localisation data.
2. Record a fixed physical reference point.
3. Close and restart the app.
4. Attempt localisation from several positions in the room.
5. Display a virtual marker at the reference point.
6. Record localisation time, position error, and failures.

## Questions
- Can the saved localisation data be loaded through our Unity/iOS integration?
- How is its coordinate system related to the RoomPlan geometry?
- What happens when the camera points down?
- What happens when tracking is interrupted?
- How will this extend to connected rooms and another floor?