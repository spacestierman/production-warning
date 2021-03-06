# Production Warning
A Windows Service that has an HTTP listener.  It listens for /start and /stop.  /start triggers the show. /stop stops the show.

# Setup
1. Build the project in Visual Studio
1. Open up the Visual Studio command prompt in administrator mode
1. `cd` into the ~/ProductionWarning.Service/bin/[Debug|Release]/ folder
1. Run `installutil ProductionWarning.exe`
1. To un-install the service, run `installutil -u ProductionWarning.exe`
1. Hit the HTTP endpoints. `http://localhost:8080/start` and `http://localhost:8080/stop`.

# Notes
- If you installed the Windows Service project using the projects' build folder *and* the service is still running, Visual Studio will be unable to overwrite the files because they're in use by the service.  Stop the service and re-build.
- App.config controls most of the variables.
- The service must run as an administrator account. If you use your personal credentials, make sure that you update the service credentials whenever you change your password.
