# MonolithApi

A simple HTTP wrapper for the [Monolith CLI](https://github.com/Y2Z/monolith).

Allows returning bundled single HTML file from a website by an API request, e.g.:

```bash
curl -X POST http://localhost:8080/archive \
  -H "Content-Type: application/json" \
  -d '{"url": "https://lyrics.github.io/db/P/Portishead/Dummy/Roads/"}'
```

The JSON request may be more complex, including all options available in Monolith:

```json
{
  "url": "https://lyrics.github.io/db/P/Portishead/Dummy/Roads/",
  "stdinHtml": null,
  "options": {
    "excludeAudio": false,
    "excludeCss": false,
    "excludeImages": true,
    "excludeJs": true,
    "excludeFonts": false,
    "excludeVideos": false,
    "omitFrames": false,
    "isolate": true,
    "noscript": false,
    "mhtml": false,
    "noMetadata": true,
    "ignoreNetworkErrors": true,
    "acceptInvalidCerts": false,
    "quiet": true,
    "timeoutSeconds": 30,
    "userAgent": "Monolith-API/1.0",
    "baseUrl": null,
    "allowDomains": [],
    "blockDomains": [],
    "cookiesFile": null,
    "encoding": null
  }
}
```
