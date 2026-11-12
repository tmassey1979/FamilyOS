# FamilyOS mobile app

- This app uses Expo SDK 58, React 19, and React Native 0.88.
- Run `npm install` from this directory after changing dependencies.
- Run `npm run typecheck` and `npx expo-doctor` before proposing changes.
- Use `npx expo install` for Expo and React Native native-module dependencies so versions match the SDK.
- The project uses Expo Router and Continuous Native Generation; native `ios/` and `android/` projects are generated during prebuild/EAS Build.

## Expo MCP

- Local Expo MCP capabilities are provided by the `expo-mcp` development dependency.
- Start the app's local MCP-enabled dev server with `npm run start:mcp` (or `npm start` without MCP).
- The local server exposes simulator/devtools interaction capabilities to a compatible remote Expo MCP client. Restart or reconnect that client after starting or stopping the dev server.
- The remote Expo MCP endpoint is `https://mcp.expo.dev/mcp` and uses OAuth. For Codex, register it with `codex mcp add expo --url https://mcp.expo.dev/mcp`, then authenticate with `codex mcp login expo`. Other MCP clients need their own remote-server configuration.
- Expo MCP setup guide: https://docs.expo.dev/mcp/
- SDK 58 docs: https://docs.expo.dev/versions/v58.0.0/