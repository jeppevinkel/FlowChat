# FlowChat

FlowChat is a Discord bot focused on high-quality music playback and voice channel interactions. Originally developed as a chat bot, it has evolved to prioritize music functionality and voice service features.

### Core Features

- **Music Playback**: Search and stream audio directly from YouTube with support for volume control, queuing, and track skipping.
- **Voice Service**: Advanced audio mixing and queue management for seamless playback in Discord voice channels.
- **Persistent Memory**: A memory management system that tracks user preferences, such as music tastes and general facts, to provide a personalized experience.
- **Text-to-Speech**: Integrated TTS capabilities for voice channel communication.
- **Dice System**: Built-in RPG dice rolling support with advantage and disadvantage mechanics.

### Technical Overview

The project is built on .NET 10 and utilizes the Discord.Net library for API interactions. It leverages FFmpeg and libdave for audio processing and Opus encoding to ensure reliable voice performance.

### Example Docker Compose

You can use the following `compose.yaml` to run the bot with a file manager for easy access to configuration and memories:

```yaml
services:
  FlowChat:
    restart: unless-stopped
    image: ghcr.io/jeppevinkel/flow-chat
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - ELEVENLAB_API_KEY=${ELEVENLAB_API_KEY}
      - ELEVENLABS_VOICE_ID=${ELEVENLABS_VOICE_ID}
    volumes:
      - flowchat-config:/app/config
      - flowchat-memories:/app/memories
networks: {}
volumes:
  flowchat-config: null
  flowchat-memories: null
```
