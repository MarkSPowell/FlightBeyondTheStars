// Flight Beyond the Stars - human dialogue
// Entry points (chosen by the game): settlement
//
// Human settlements on Haven, met by driving the rover into a colony town. Unnamed colonists; trading is done on
// the game's market screen. Conventions and game-provided functions/variables: see orrun.ink.

EXTERNAL rep(delta)
EXTERNAL heard(id)
EXTERNAL knows(word)
EXTERNAL has_artifact(id)

VAR posture = "friendly"
VAR comms = 60
VAR credits = 0
VAR reputation = 0
VAR in_combat = false
VAR odds = 100
VAR they_hailed = false

-> settlement

=== settlement ===
{ settlement == 1:
    A tidy colony town of white domes and walkways. A woman in a dusty survey jacket waves your rover into a parking bay painted with faded numbers.
    Colony steward: Captain! We don't get many ship crews driving in. Usually you lot just fly over and wave. Welcome to the colony.
- else:
    Colony steward: Back again, Captain? Bay four's still free. It's always free; nobody else parks there.
}
-> colony_talk

= colony_talk
+ [How are things in the colony?] -> colony_mood
+ [Anything strange turned up around here?] -> colony_strange
+ { knows("exodus") || knows("void-chart") || knows("burial") } [Have you ever heard that humans came from somewhere else?] -> colony_origins
+ [Goodbye.] -> colony_bye

= colony_mood
{
- heard("stage-4"):
    Colony steward: Rationing. The supply ships can't get through and nobody will tell us why. People have started asking whether we're on our own now.
- heard("stage-3"):
    Colony steward: Prices are climbing every week. Fuel's the worst. The news calls it a "route instability". My grandmother would call it something ruder.
- heard("mother-signal"):
    Colony steward: Since that voice came over every radio? Quiet. People don't talk about it. "Return to me." It sounded like it meant us, specifically. Isn't that strange?
- heard("stage-2"):
    Colony steward: A bit nervous. A few of the long-haul routes have gone dark. Probably nothing. That's what everyone says, so it must be true, right?
- else:
    Colony steward: Good, honestly. Harvest's in, the kids are loud, and the survey office pays on time. Can't ask for more.
}
-> colony_talk

= colony_strange
{ heard("first-artifact"):
    Colony steward: Since word got round about a ship finding a real alien artifact, half the colony's out digging in the old ruins with garden spades. All anyone's found is rocks and a very angry burrowing thing.
- else:
    Colony steward: Strange? There are old ruins in the hills nobody's ever properly studied. The university says they're "pre-colonial". Pre-us, I mean. Nobody seems very curious about who built them.
}
-> colony_talk

= colony_origins
The steward laughs, then stops when she sees your face.
Colony steward: Came from somewhere else? We've always been here, Captain. Everyone knows that. It's the first thing they teach you.
Colony steward: ...Funny. That's exactly how they teach it, too. "We have always been here." Word for word. I never thought about how odd it is to teach a thing like that.
-> colony_talk

= colony_bye
Colony steward: Safe roads, Captain. Mind the ridge road, it eats rovers.
-> END
