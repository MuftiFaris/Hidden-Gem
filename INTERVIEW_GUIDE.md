# Interview Assistant Guide

This guide explains how to use the **Interview Assistant** feature for real-time interview support.

## What It Does

The Interview Assistant helps you during live video interviews (Zoom, Discord, GMeet, Teams) by:

1. **Capturing audio** from what the interviewer is saying
2. **Detecting questions** automatically (or manually)
3. **Generating intelligent responses** using Gemini AI
4. **Providing context** by analyzing your screen
5. **Displaying answers** for you to review and use

## How to Use

### Step 1: Prepare

1. Open Hidden Gem
2. Go to **Settings**
3. Ensure your Gemini API key is configured and tested
4. Return to main interface

### Step 2: Join Your Interview

1. Join Zoom/Discord/GMeet/Teams call
2. Share your screen or have it ready
3. Position Hidden Gem window where you can see responses

### Step 3: Start Interview Assistant

1. Click **Interview** tab
2. Click **🎤 Start Listening**
3. Status should show "🎤 Listening for questions..."

### Step 4: Enable Auto-Response (Optional)

- Check **Enable auto-response** to automatically generate answers
- Or manually copy-paste questions into Chat tab

### Step 5: Respond During Interview

#### Automatic Mode (Auto-Response Enabled)
1. Wait for system to detect your question
2. AI generates response automatically
3. Review in "CURRENT RESPONSE" box
4. Click **📋 Copy Response**
5. Paste into chat/type naturally

#### Manual Mode (Auto-Response Disabled)
1. Click **Start Listening** to capture audio
2. When question is asked, click **Stop** to end capture
3. Copy the detected question
4. Paste into Chat tab
5. Get AI response
6. Use response as reference

### Step 6: Stop When Done

1. Click **⏹️ Stop** to stop listening
2. Review **EXCHANGE HISTORY**
3. Close Interview tab when call ends

## Important Features

### Audio Capture

- **How it works**: Captures audio playing through your speakers (WASAPI loopback)
- **Supported**: Zoom, Discord, GMeet, Teams, OBS output, any app with audio
- **Requirements**: Windows 10 2004+ with WASAPI support (all modern Windows)

### Question Detection

The system detects questions by pattern matching (regex):

**Default patterns:**
- "Tell me about..."
- "How would you..."
- "What is your..."
- "Can you explain..."
- "Why did you..."
- "What's your experience with..."

### Response Generation

1. **Captures current screen** for visual context
2. **Transcribes the question** from audio
3. **Sends to Gemini**: "Person asked this on screen [screenshot]. Person said: [question]. Answer it."
4. **Generates response** with screen context
5. **Displays for review**

### Audio Quality

- Works best with clear speech
- Filters out background noise automatically
- Handles multiple speakers (captures all audio)
- Adapts to different languages

## Pro Tips

### 1. Test Before Interview
- Run a practice session with a friend
- Test audio capture
- Verify question detection
- Check response quality

### 2. Use Chat Tab for Detail
- Interview Assistant is for quick reference
- For detailed responses, use Chat tab
- Copy detected question → Chat → Get full response

### 3. Context Matters
- What's on your screen affects response quality
- Keep relevant documents visible (resume, portfolio)
- Close sensitive content first

### 4. Review Before Using
- Always read generated responses
- AI makes mistakes sometimes
- Adapt responses to your situation
- Don't copy verbatim - sound natural

### 5. Privacy
- Responses are NOT stored
- Audio is NOT saved
- Only sent to Gemini API temporarily
- Screen capture is local-only

### 6. Keyboard Shortcuts
- **Ctrl+C**: Copy response (when focused on response box)
- **Alt+Tab**: Switch between apps quickly
- **Alt+Tab** then **Interview**: Switch back to interviewer view

## Troubleshooting

### Audio Not Captured

**Problem**: "Listening" but no audio detected

**Solutions**:
1. Check speaker volume is not muted
2. Verify Windows has permission to capture audio
3. Try different audio device (if multiple speakers)
4. Restart app and try again

### Question Not Detected

**Problem**: Audio captured but question not recognized

**Solutions**:
1. Question might not match default patterns
2. Audio quality too low (background noise)
3. Question in different language than English
4. Try manual mode: copy question to Chat tab

### Responses Are Generic

**Problem**: Generated answers lack specificity

**Solutions**:
1. Have your resume/portfolio visible on screen
2. Use Chat tab for detailed responses
3. Add context manually (copy question → Chat → add context → send)
4. AI works better with visual context

### Slow Response Generation

**Problem**: Takes 5+ seconds to generate response

**Solutions**:
1. Check internet connection
2. Gemini API might be rate-limited (15 req/min free tier)
3. Screen capture might be slow (try smaller resolution)
4. Try without screen context (might be faster)

### App Crashes

**Problem**: Interview Assistant crashes or hangs

**Solutions**:
1. Stop listening (click Stop button)
2. Close Interview tab
3. Restart app
4. Check logs: `%LOCALAPPDATA%\HiddenGem\logs\`
5. Check .NET 8.0 installed correctly

## Limitations & Considerations

### Audio Capture Limitations
- Requires Windows 10 2004 or newer
- Some audio drivers don't support WASAPI loopback
- Works on laptop audio, not always with USB devices
- External recorders (hardware) may need workarounds

### AI Limitations
- Responses may lack nuance
- Doesn't understand company-specific context
- Can't browse real-time resources
- Training data has cutoff date (check Gemini docs)

### Interview Etiquette
- Use as **reference only**, not verbatim answers
- Practice sounding natural
- Don't rely entirely on AI responses
- Some interviews detect screen sharing/recording

## Interview Scenarios

### Scenario 1: Technical Interview
1. Have code editor visible
2. Share your portfolio/projects on screen
3. Let AI analyze code when asked
4. Use responses as talking points
5. Follow up with real implementation details

### Scenario 2: Behavioral Interview
1. Have resume/work history visible
2. Let AI generate story-based responses
3. Personalize with your specific examples
4. Don't sound robotic (paraphrase)
5. Mix AI suggestions with your experiences

### Scenario 3: Phone Interview (No Video)
1. Use Chat tab instead
2. Manual question entry
3. More flexibility without screen sharing
4. Better for detailed explanations

### Scenario 4: Group Interview
1. Captures audio from all speakers
2. May detect multiple questions
3. Slower response generation
4. Best used for initial questions only

## Advanced Usage

### Custom Question Patterns

(Feature for future versions)

You'll be able to add custom regex patterns to detect specific questions:

```
"What frameworks have you used"
"Tell me about a time you"
"How do you approach"
```

### Context Documents

Have reference materials ready:

- Resume/CV
- Portfolio samples
- Project descriptions
- Tech stack you know
- Company info
- Job description

Position them on screen for AI to analyze.

### Integration with Chat

1. Detect question in Interview tab
2. Copy to Chat tab
3. Ask for detailed explanation
4. Get comprehensive response
5. Use best parts for answer

## Settings

No special settings needed, but ensure:

- ✅ API key is valid and tested
- ✅ Microphone permission granted
- ✅ App has speaker access
- ✅ Audio device has volume
- ✅ Internet connection stable

## FAQ

**Q: Will interviewers know I'm using this?**
A: No - responses are displayed locally on your screen. Just don't share your screen during response review.

**Q: Is this cheating?**
A: Use responsibly. AI should be reference/thinking tool, not verbatim answers. Adapt responses to sound natural.

**Q: What if audio doesn't capture?**
A: Most modern Windows supports WASAPI. If not, use Chat tab instead.

**Q: Can I record the interview?**
A: Don't record without consent. But responses are logged in Exchange History for your reference.

**Q: How accurate are transcriptions?**
A: Usually 90%+ with clear speech. Background noise can reduce accuracy.

**Q: What languages does it support?**
A: Any language Gemini supports (50+). Works best with English.

## Support

- **Issue**: Check logs: `%LOCALAPPDATA%\HiddenGem\logs\`
- **API Error**: See Settings tab for API key validation
- **Crash**: Check GitHub Issues
- **Feature Request**: Create issue with details

## Next Steps

1. Read [README.md](README.md) for general features
2. Check [DEPENDENCIES.md](DEPENDENCIES.md) for tech details
3. Review [Settings](SETTINGS.md) for configuration
4. Start practicing with test interviews!

---

**Good luck with your interviews!** 🎯

Remember: AI is a tool, not a replacement for your skills and knowledge.
