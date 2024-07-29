# MarkovBot

A discord bot that indexes messages into a Graph DB and then uses markov to generate new messages from the indexed messages.

## FAQ

1. **Q:** Can a message history be retrieved from this?

   **A:** No, it only stores individual words and the amount of times they were used in a server, the only way a message history can be retrieved from this is if every message didn't share too many words with each other.
2. **Q:** Can this load the message history?

   **A:** No, this would be against Discord's App ToS as it forbids scraping for data.
3. **Q:** How do I restrict which channels it indexes?

   **A:** Just don't let the bot see that channel using permissions and it won't index messages from it.
