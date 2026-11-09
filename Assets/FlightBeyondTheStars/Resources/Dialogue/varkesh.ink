// Flight Beyond the Stars - Varkesh dialogue
// Entry points (chosen by the game): homeworld, settlement, encounter
//
// The Varkesh are the opposite of the Orrun: they respect strength and plain speech, and they read
// flattery as cowardice or a hidden knife. Hostile posture earns respect (within reason); obsequious
// costs it. Their clans swore an ancient Oath against "silent ships" that close worlds. The ships
// never came - until the Anchors start cutting the rim.
//
// Conventions and game-provided functions/variables: see orrun.ink.

EXTERNAL rep(delta)
EXTERNAL pay(amount)
EXTERNAL receive(amount)
EXTERNAL learn(word)
EXTERNAL knows(word)
EXTERNAL give_lore(race)
EXTERNAL survey_data()
EXTERNAL heard(id)
EXTERNAL days_since(id)
EXTERNAL attack()
EXTERNAL stand_down()
EXTERNAL crew_reacts(event)
EXTERNAL has_artifact(id)
EXTERNAL give_artifact(id)

VAR posture = "friendly"
VAR comms = 60
VAR credits = 0
VAR reputation = 0
VAR in_combat = false
VAR odds = 100
VAR they_hailed = false

// Trial of nerve score for the current attempt
VAR nerve = 0

-> homeworld

=== function shift(delta) ===
~ reputation = rep(delta)

=== function bold() ===
~ return posture == "hostile"

=== function fawning() ===
~ return posture == "obsequious"

// How the Varkesh take the captain's tone: plain speech is fine, strength is better, grovelling is worse.
=== function tone() ===
{
- bold():
    ~ shift(2)
- fawning():
    ~ shift(-3)
}

// ------------------------------------------------------------------ the homeworld

=== homeworld ===
{ reputation <= -30:
    Clanmaster Ashquill: You crawled to us once and lied to us twice. The next time our ships see yours, they will not use their radios.
    The channel closes with what sounds like a blade being sheathed.
    -> END
}
{ homeworld > 1:
    -> greet_again
}
A tall, hawk-like Varkesh in scarred plate fills the screen, her crest of quills half raised. Behind her, a hall of standing stones, each carved with names.
Clanmaster Ashquill: I am Ruuk Ashquill, Clanmaster of the Ashquill clan and keeper of the Oath. An unknown ship hails me instead of hiding. Brave or foolish. Say which.
{
- bold():
    Clanmaster Ashquill: Ha! You bare your teeth at me from your own bridge. Good. Cowards always start with compliments.
    ~ shift(5)
- fawning():
    Clanmaster Ashquill: You bow to a face on a screen? Stand up, stranger. We do not trust anything that bends that easily.
    ~ shift(-5)
- else:
    Clanmaster Ashquill: Plain words. Acceptable. Speak.
}
-> news ->
-> topics

= greet_again
{
- reputation >= 30:
    Clanmaster Ashquill: Shield-friend! The stones remember your name. Sit at our fire, even if it is only a screen.
- reputation >= 0:
    Clanmaster Ashquill: The human captain returns. Speak plainly.
- else:
    Clanmaster Ashquill: You again. Say what you came to say, and say it quickly.
}
-> news ->
-> topics

= topics
+ [Who are the Varkesh?] -> about_varkesh
+ { knows("oath") } [What exactly did you swear the Oath against?] -> the_oath
+ { not trial_won } [How does an outsider earn Varkesh respect?] -> trial
+ { knows("houses") || knows("varkesh") } [What do you think of the Orrun?] -> orrun
+ { knows("varkesh-3") && not the_blade } [We found a Varkesh blade made for a human hand.] -> the_blade
+ { the_blade } [Tell me more about the humans who stood with you.] -> the_watch
+ { has_artifact("first_watch_sigil") && not the_sigil } [Show her the Sigil of the First Watch.] -> the_sigil
+ [Any news?] -> recap
+ [We're done here.] -> goodbye

// ------------------------------------------------------------------ topics

= about_varkesh
{ about_varkesh > 1:
    Clanmaster Ashquill: Clans. Stones. The Oath. I have told you already. We do not need to repeat ourselves; we are not Orrun.
    -> topics
}
Clanmaster Ashquill: We are twelve clans and one Oath. Every Varkesh born is carved on a stone, and every stone stands in this hall.
Clanmaster Ashquill: Long ago our ancestors swore to stand against the silent ships. Every generation swears it again, so the Oath never grows old.
{ comms >= 100:
    Comms Officer: Captain, that word, "silent ships". It's not a figure of speech. They use the same word for a ship with its transponder off.
}
~ tone()
~ learn("oath")
-> topics

= the_oath
Clanmaster Ashquill: Ships that do not speak. They do not burn worlds. They close them. One door, then another, until no one can leave.
Clanmaster Ashquill: Our songs say they came once, before the clans had names. Something drove them off. We swore that next time, it would be us.
{ heard("stage-2"):
    Clanmaster Ashquill: And now the rim goes dark, one lane at a time. The songs were not stories, human.
- else:
    Clanmaster Ashquill: Every other race laughs at us for it. Let them laugh. We have been ready for ten thousand years.
}
{ fawning():
    Clanmaster Ashquill: And do not tell me how noble it is. I can hear you deciding we are quaint.
    ~ shift(-2)
}
-> topics

= orrun
Clanmaster Ashquill: The Orrun would sell their own shadows and charge the buyer for the dark.
Clanmaster Ashquill: But they have never once broken a contract with us. Not once. That is a kind of honour, I suppose. A boring kind.
-> topics

= the_blade
Clanmaster Ashquill is silent for a long moment. Her quills lie flat. The hall behind her has gone quiet too.
Clanmaster Ashquill: Then it is true. The first stone in this hall bears no Varkesh name. It bears a human handprint.
Clanmaster Ashquill: When our ancestors first swore the Oath, your kind stood beside them. A watch of humans, the songs say. They forged blades for us that no forge of ours can match.
Clanmaster Ashquill: Then one day they were gone, and we kept the Oath alone. We have never forgiven you for that. We have never stopped hoping you would come back, either.
{ comms >= 150:
    Comms Officer: (quietly) Captain, the word she used for "watch" is the one from the Orrun ledger. The First Watch.
}
~ learn("first-watch")
~ shift(5)
-> topics

= the_watch
Clanmaster Ashquill: The songs say little. They were few. They were tired, as if they had already fought this war somewhere else and lost.
Clanmaster Ashquill: If you want more, ask the Orrun. They were here before the clans had names, and they write down everything they are paid to forget.
-> topics

= the_sigil
You hold the black sigil up to the screen. Every Varkesh in the hall behind Ashquill stops moving.
Clanmaster Ashquill: Where did you find that?
Clanmaster Ashquill: (very quietly) "We stand the watch together, until the silent ships come." Those are the last words of the Oath. Only the clanmasters are taught them.
{ not the_blade:
    Clanmaster Ashquill: The first stone in this hall bears a human handprint, stranger. Your kind stood with us, once. Then they were gone.
    ~ learn("first-watch")
}
Clanmaster Ashquill: That sigil belongs on the first stone. I will not take it from you. I will ask for it.
+ [It belongs on your stone. Take it.]
    { give_artifact("first_watch_sigil"):
        The Clanmaster takes the sigil in both hands and bows her crest, something you have never seen a Varkesh do.
        Clanmaster Ashquill: Then the watch is stood again. Your name will be cut beside the handprint, Captain. Every clan ship will know it.
        ~ shift(25)
        ~ learn("oath-kin")
        ~ crew_reacts("Mercy")
    }
    -> topics
+ [I need to keep it, for now.]
    Clanmaster Ashquill: Then keep it well. A thing like that does not stay lost twice.
    ~ shift(2)
    -> topics

// ------------------------------------------------------------------ the trial of nerve

= trial
{ trial > 1 && not trial_won:
    Clanmaster Ashquill: You want to try again? Good. Losing once is not shameful. Losing twice the same way is.
- else:
    Clanmaster Ashquill: Respect is earned at the stones. I ask three questions. Answer like a captain, not like a merchant.
}
~ nerve = 0
-> q1

= q1
Clanmaster Ashquill: First. My war band is between your ship and the jump lane. What do you do?
+ [Fly straight through you.]
    ~ nerve += 1
    Clanmaster Ashquill: Ha. Reckless. But not wrong.
+ [Hail you and ask you to move.]
    Clanmaster Ashquill: Polite. We would have moved. We would also have laughed about it for a year.
+ [Turn around and find another lane.]
    ~ nerve -= 1
    Clanmaster Ashquill: And if there is no other lane, stranger?
- -> q2

= q2
Clanmaster Ashquill: Second. One of your crew disobeys you in battle, and it saves the ship.
+ [I thank them, then I put them on report.]
    ~ nerve += 1
    Clanmaster Ashquill: Both. Yes. That is how a clan stays alive.
+ [I promote them.]
    Clanmaster Ashquill: Generous. Your crew will remember the lesson that disobeying pays.
+ [I throw them off my ship.]
    ~ nerve -= 1
    Clanmaster Ashquill: Then you have a smaller crew and a fool for a captain.
- -> q3

= q3
Clanmaster Ashquill: Last. The silent ships come for your home. You cannot win. What do you do?
+ [Fight anyway.]
    ~ nerve += 1
    Clanmaster Ashquill: (she strikes the stone beside her with her fist, quills rising) Yes.
+ [Find a way to win that nobody has tried.]
    ~ nerve += 1
    Clanmaster Ashquill: Hm. A human answer. The songs say your kind answered like that once, too.
+ [Get as many people out as I can.]
    Clanmaster Ashquill: Honourable. Not Varkesh. But honourable.
+ [Surrender, if it saves lives.]
    ~ nerve -= 2
    Clanmaster Ashquill: Then you have already lost, and you will never know if you could have won.
- -> trial_result

= trial_result
{ nerve >= 2:
    -> trial_won
- else:
    Clanmaster Ashquill: Not yet, human. Come back when you have thought about the answers, not the questions.
    ~ shift(-1)
    -> topics
}

= trial_won
Clanmaster Ashquill: Enough. You answer like someone who has stood in front of something bigger than you and did not run.
Clanmaster Ashquill: Take this. Our record-keepers will send you a page from the oldest songs. Few outsiders hear them.
~ temp title = give_lore("Varkesh")
{ title != "":
    Clanmaster Ashquill: "{title}". Read it. Then you will understand why we do not laugh at the dark.
}
~ shift(15)
-> topics

= recap
{ not any_news():
    Clanmaster Ashquill: News? Our ships patrol, our stones stand, the Oath holds. That is all the news the Varkesh need.
    -> topics
}
{
- heard("stage-5"):
    Clanmaster Ashquill: Your home is closed. Every Varkesh ship that can still fly is being fitted for war.
- heard("stage-4"):
    Clanmaster Ashquill: The galaxy is cut into pieces. We fight in every piece we can still reach.
- heard("stage-3"):
    Clanmaster Ashquill: The great lanes are falling. The Oath is no longer something we swear. It is something we do.
- heard("stage-2"):
    Clanmaster Ashquill: The rim is dark. The silent ships have come, as the songs said.
}
{ heard("first-anchor"):
    Clanmaster Ashquill: One of their stations sits on a dead lane. We have sent warriors. We will send more.
}
{ heard("mother-signal"):
    Clanmaster Ashquill: And a voice still calls on every band, telling us to return. Varkesh do not return. We advance.
}
-> topics

= goodbye
{
- bold():
    Clanmaster Ashquill: Go, then. Fly like that in a fight and you may yet live long.
- fawning():
    Clanmaster Ashquill: Go. And next time, stand up straight when you speak to me.
- else:
    Clanmaster Ashquill: Go well, human. Keep your shields charged.
}
-> END

// ------------------------------------------------------------------ world events
// The Varkesh comment on each event once, the first time we talk after news reaches their home.

=== function any_news() ===
~ return heard("stage-2") || heard("first-anchor") || heard("mother-signal")

=== news ===
{ heard("mother-signal") && not mother_signal:
    -> mother_signal ->
}
{ heard("first-anchor") && not first_anchor:
    -> first_anchor ->
}
// Only the latest threat stage the Varkesh have heard of gets a comment.
{
- heard("stage-5"):
    { not stage5:
        -> stage5 ->
    }
- heard("stage-4"):
    { not stage4:
        -> stage4 ->
    }
- heard("stage-3"):
    { not stage3:
        -> stage3 ->
    }
- heard("stage-2"):
    { not stage2:
        -> stage2 ->
    }
}
{ heard("first-artifact") && not first_artifact:
    -> first_artifact ->
}
->->

= mother_signal
Clanmaster Ashquill: You heard it. "Return to me." Every receiver on our world, at once.
Clanmaster Ashquill: The young ones are frightened. The old ones are not. The old ones have been waiting for that voice their whole lives, and it has finally given them something to shout back at.
{ comms >= 150:
    Comms Officer: Captain, the clans are already broadcasting a reply on the same band. It translates as: "Come and take us."
}
->->

= first_anchor
Clanmaster Ashquill: A war band of Clan Durrek flew to the dead lane to see the station that sits there.
Clanmaster Ashquill: Their last message was a single word: "silent." Then nothing. Clan Durrek has carved twelve new names on its stone.
~ learn("anchor")
->->

= stage2
{ days_since("stage-2") > 30:
    Clanmaster Ashquill: The rim has been dark for more than a month. You took your time coming to talk about it.
}
Clanmaster Ashquill: The rim worlds have gone silent, one lane after another. Not burned. Closed.
Clanmaster Ashquill: Ten thousand years we have sworn the Oath, and the others laughed. Nobody is laughing now.
Clanmaster Ashquill: I find I wish they still were.
~ learn("rim-dark")
->->

= stage3
Clanmaster Ashquill: The great lanes are falling. Our patrols return with half their ships, and the half that return have nothing to fight. The enemy does not come out. It only closes doors.
Clanmaster Ashquill: How do you fight something that will not stand in front of you, human? Our songs never said.
->->

= stage4
Clanmaster Ashquill: The galaxy is in pieces. Some of our clans are on the other side of a wall of dead lanes now. We can hear them. We cannot reach them.
Clanmaster Ashquill: We still carve their names. Every morning.
->->

= stage5
Clanmaster Ashquill: Your home space is closed. We heard the same hour you did.
Clanmaster Ashquill: The Oath was sworn beside your kind once. Whatever you do next, human, you will not do it alone. That is a promise on the first stone.
~ shift(10)
->->

= first_artifact
{ reputation >= 0:
    Clanmaster Ashquill: The Orrun are telling everyone that you dug something old out of the ground. They tell everyone everything, for a price.
    Clanmaster Ashquill: If it is a weapon, do not sell it to them. Bring it here. We will not pay as much. We will use it better.
}
->->

// ------------------------------------------------------------------ ship encounters
// A Varkesh patrol. Below zero standing they offer a fight; otherwise they ask your business.
// They never retreat once committed, but they accept an honest yield or a hard-won draw.

=== encounter ===
{ in_combat:
    -> mid_fight
}
A narrow Varkesh cockpit, red-lit. The pilot's quills are fully raised.
Wing-leader Korr: Human ship. You fly in Ashquill space.
{ reputation < 0:
    -> challenge
- else:
    -> business
}

= challenge
Wing-leader Korr: Your name is cut on no stone of ours, and the clans speak poorly of you. Turn your ship around, or turn your guns on us.
+ [Then we fight.]
    Wing-leader Korr: Good. At least you are not a coward.
    ~ shift(2)
    ~ attack()
+ { knows("oath-kin") } [Read the stones of the Ashquill hall. My name is cut beside the handprint.]
    The pilot checks something off-screen. His quills drop flat.
    Wing-leader Korr: ...It is. Forgive the challenge, oath-kin. Fly where you like.
    ~ shift(3)
    ~ stand_down()
+ { not fawning() } [We mean no harm. We're leaving.]
    Wing-leader Korr: Then leave. Do not come back without a reason.
    ~ stand_down()
+ { fawning() } [Please, honoured warriors, we beg you to let us pass!]
    Wing-leader Korr: Begging. Disgusting. Weapons free.
    ~ shift(-2)
    ~ attack()
- -> END

= business
Wing-leader Korr: State your business.
+ [Trade and exploration.]
    Wing-leader Korr: Then trade honestly and explore carefully. Fly on.
+ { heard("stage-2") } [We're hunting the silent ships.]
    Wing-leader Korr: Then we hunt the same prey. Good hunting, human.
    ~ shift(3)
+ [None of your business.]
    { bold():
        Wing-leader Korr: Ha! Fair. Fly on, sharp-tongue.
        ~ shift(1)
    - else:
        Wing-leader Korr: Then fly on quickly.
    }
+ { fawning() } [Your pardon, mighty warriors, we are honoured to meet you...]
    Wing-leader Korr: Stop bowing. Go.
    ~ shift(-2)
- ~ stand_down()
-> END

= mid_fight
Wing-leader Korr: You hail me in the middle of a fight? Speak quickly.
+ [We yield.]
    Wing-leader Korr: Yielding is honest. Fleeing is not. Go, and remember who let you live.
    ~ shift(-3)
    ~ crew_reacts("Fled")
    ~ stand_down()
+ [Let's call this a draw.]
    { odds >= 120:
        Wing-leader Korr: ...You fight well, human. A draw, then. Our stones will remember you.
        ~ shift(5)
        ~ stand_down()
    - else:
        Wing-leader Korr: A draw? You are losing. No.
    }
+ [Nothing. Weapons free.]
    Wing-leader Korr: Ha!
- -> END

// ------------------------------------------------------------------ a clan hold on Kesh
// Unnamed locals, met by driving the rover up to a hold. Trading is done on the game's market screen.

=== settlement ===
{ reputation <= -30:
    A warrior on the wall levels a long gun at your rover and says nothing at all. The gate stays shut.
    -> END
}
{ settlement == 1:
    The outer yard of a clan hold: black stone walls, a forge roaring in one corner, children sparring with blunt blades. A broad-shouldered warden with a scarred crest strides out to meet your rover.
    Hold-warden: A human, driving up to our gate in a little cart. The children will talk about this for a year. State your business.
- else:
    { knows("oath-kin"):
        Hold-warden: Oath-kin! Come in, come in. The forge is hot and the stew is worse. Both are yours.
    - else:
        Hold-warden: The human in the little cart, again. Speak.
    }
}
{
- bold():
    Hold-warden: Ha! You stare a warden down from a cart. Good.
    ~ shift(1)
- fawning():
    Hold-warden: Stop bowing. You look like a bird drinking.
    ~ shift(-2)
}
-> hold_talk

= hold_talk
+ [What news in the hold?] -> hold_mood
+ [Do you trade?] -> hold_trade
+ { has_artifact("first_watch_sigil") && not hold_sigil } [Show the warden the black sigil.] -> hold_sigil
+ [Farewell.] -> hold_bye

= hold_mood
{
- heard("stage-3"):
    Hold-warden: The clan ships go out and fewer come back. The silent ships are real, human. The Oath was not a story.
- heard("first-anchor"):
    Hold-warden: The Clanmaster has called the far holds home. Something at the rim cuts the lanes, and the old ones are sharpening blades they have not touched in a hundred years.
- heard("stage-2"):
    Hold-warden: Talk of failing lanes. The young ones want to fly out and fight the dark. The old ones say wait. The old ones are usually right, and always annoying.
- else:
    Hold-warden: Quiet. Good forging weather. The trial stones are booked for a month.
}
-> hold_talk

= hold_trade
Hold-warden: We trade. We do not haggle. Name a fair price, take a fair price. The Orrun think this makes us easy marks. The Orrun are welcome to try it in person.
Hold-warden: Weapons and good steel we always want. Old relics of war, bring to the Clanmaster, not to me.
-> hold_talk

= hold_sigil
The warden looks at the sigil, then drops to one knee in the dust of the yard. The sparring children stop.
Hold-warden: That is the watch's mark. That belongs on the Clanmaster's first stone, human. Take it to her. Do not sell it. Do not even think about selling it.
-> hold_talk

= hold_bye
Hold-warden: Go well. Mind the cliffs; your cart looks breakable.
-> END
