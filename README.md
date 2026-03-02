# MediaEngine

This is media utility for warwakei projects allowing to check current media playing on PC. As youtube videos, spotify in browser, etc. Just using something as windows media session API but without WinRT (i hate WinRT).

## Used In

- [SimpleChatbox](https://github.com/warwakei/SimpleChatbox) - VRChat chatbox utility for displaying currently playing media

## Features

- Get current track information (title, artist, display name)
- Check if media is paused or playing
- Configurable delay for polling
- Simple REST API on localhost:5000
- CORS enabled for cross-origin requests

## API Endpoints

### Get Current Track
```
GET /api/track
```

Returns JSON with current track information:
```json
{
  "title": "Song Title",
  "artist": "Artist Name",
  "display": "Artist Name - Song Title",
  "isPaused": false,
  "delay": 3000
}
```

### Set Delay
```
POST /api/delay?delayMs=5000
```

Set polling delay in milliseconds (100-60000 ms).

### Get Status
```
GET /api/status
```

Returns service status:
```json
{
  "version": "0.01",
  "status": "running"
}
```

## Usage

Run the executable:
```
MediaEngine.exe
```

The service will start on `http://localhost:5000`

## Requirements

- Windows 10 or later
- .NET 8.0 Runtime (included in self-contained build)

## License

MIT License
