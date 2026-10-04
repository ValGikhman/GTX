# Persistent mobile login

New mobile logins remain valid until logout or an Owner password change. Sessions survive application pool restarts. The existing Android app already saves the opaque token in SecureStore; no new APK is required.

The server stores token hashes and HMAC password verifiers in `App_Data/MobileSessions`. It does not store bearer tokens or passwords. Keep this directory private, writable by the application pool, and preserved during deployment. All workers must use the same directory. Deleting its session files revokes access. Clearing phone app data or uninstalling requires signing in again.

Deploy the updated GTX assembly using the normal website deployment process. Existing eight-hour sessions require one new login after deployment. The hosted site has not been updated by this local change.

Validation: `powershell -ExecutionPolicy Bypass -File tests/mobile-sessions.ps1`, followed by a Release build. After deployment, check login, authenticated inventory, logout/rejection of the old token, and session survival across an application pool recycle.
