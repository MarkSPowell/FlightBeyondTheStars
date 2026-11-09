// Flight Beyond the Stars - Orrun Combine dialogue
// Entry points (chosen by the game): station, homeworld, settlement, encounter
//
// Conventions the game understands:
//   "Name: text"      a line spoken by Name (anything else is narration)
//   [bracketed choice text] is what the captain says; the game echoes it into the transcript
//
// Variables the game sets before each conversation and when the captain changes posture:
//   posture     "friendly" | "obsequious" | "hostile"
//   comms       Communications Officer skill (0-250)
//   credits     ship's credits (whole number)
//   reputation  Orrun opinion of us (-100 to 100)

EXTERNAL rep(delta)          // change reputation, returns the new value
EXTERNAL pay(amount)         // true if we could afford it (credits are taken)
EXTERNAL receive(amount)     // give the ship credits
EXTERNAL learn(word)         // add a conversation keyword
EXTERNAL knows(word)         // keyword known, or lore record with that id found
EXTERNAL give_lore(race)     // hand over the next unfound lore record; returns its title or ""
EXTERNAL survey_data()       // mark the nearest unexplored system as surveyed; returns its name or ""
EXTERNAL heard(id)           // true once news of a world event (Events tab) has reached the Orrun
EXTERNAL days_since(id)      // days since a world event happened, or -1 if it hasn't
EXTERNAL attack()            // ship encounters: their ship opens fire
EXTERNAL stand_down()        // ship encounters: their ship stops being hostile and leaves
EXTERNAL crew_reacts(event)  // the crew's morale reacts to what the captain did (MoraleEvents tab id)
EXTERNAL has_artifact(id)    // true if this major artifact (Artifacts tab id) is aboard and identified
EXTERNAL give_artifact(id)   // hand it over (quests); false if it isn't aboard

VAR posture = "friendly"
VAR comms = 60
VAR credits = 0
VAR reputation = 0

// Ship encounters (set by the game): in the middle of a fight; our strength vs theirs (100 = even);
// whether they opened the channel
VAR in_combat = false
VAR odds = 100
VAR they_hailed = false

// Haggled prices
VAR survey_price = 500
VAR history_price = 300

-> station

// ------------------------------------------------------------------ helpers

=== function shift(delta) ===
~ reputation = rep(delta)

=== function hostile() ===
~ return posture == "hostile"

=== function fawning() ===
~ return posture == "obsequious"

// Prices move with posture: courtesy is good business, hostility raises every price.
=== function quote(base) ===
{
- fawning():
    ~ return base * 9 / 10
- hostile():
    ~ return base * 3 / 2
- else:
    ~ return base
}

// ------------------------------------------------------------------ the station

=== station ===
{ reputation <= -40:
    Factor Venn: Your account with the Combine is closed, Captain. Closed accounts do not receive conversation. Conversation is a service.
    The channel goes dead.
    -> END
}
{ station > 1:
    -> greet_again
}
A stout, four-armed Orrun in a coat of many pockets fills the screen. The sac at his throat flushes a pleased gold.
Factor Venn: Welcome to Brightmarket, newcomer. I am Factor Bolo Venn, master of this station and humble servant of the Orrun Combine. Before you say anything, know that this greeting is complimentary. The second one is not.
Factor Venn: That was a joke. Maxim Twelve: the first joke is also complimentary.
{ hostile():
    Factor Venn: You have the face of a customer who intends to haggle with a weapon. Many have. Their ships are now very affordable.
    ~ shift(-5)
- else:
    { fawning():
        Factor Venn: Such courtesy! You understand how business is done. I will note it in your file, in the column for favoured customers.
        ~ shift(3)
    }
}
-> news ->
-> topics

= greet_again
{ hostile():
    Factor Venn: You again. Your tone has not improved, but your credit is still good. Speak.
- else:
    { reputation >= 30:
        Factor Venn: Captain! My favourite account. Do not tell the others; they all think they are my favourite.
    - else:
        Factor Venn: Captain. The Combine remembers you. That is not yet a compliment, but it is also not a bill.
    }
}
-> news ->
-> topics

= topics
+ [Who are the Orrun Combine?] -> about_combine
+ [What sells well here?] -> trade_tips
+ [Do you sell star charts?] -> survey_offer
+ { reputation >= 10 } [Do you know anything about ruins on these worlds?] -> history_offer
+ { knows("first-watch") || knows("humans-1") } [What do you know about the First Watch?] -> first_watch
+ { knows("varkesh") } [Tell me about the Varkesh.] -> varkesh
+ { has_artifact("founding_seal") && not seal_venn } [I found an old Orrun seal in some ruins.] -> seal_venn
+ [Any news?] -> recap
+ [That's all for now.] -> goodbye

// ------------------------------------------------------------------ topics

= about_combine
{ about_combine > 1:
    Factor Venn: Houses, accounts, debts. I explained this already, Captain. Repeating it is a second consultation.
    -> topics
}
Factor Venn: The Combine is not a nation, Captain. Nations have borders and flags and wars. We have accounts.
Factor Venn: Every Orrun belongs to a trade house, and every house belongs to the Combine, and the Combine belongs to whoever is owed the most. This changes daily. It keeps everyone polite.
{ comms >= 100:
    Comms Officer: He says "polite", Captain, but the word he chose is closer to "nervous".
}
{ hostile():
    Factor Venn: Before you ask: no, we are not afraid of you. We are afraid of being underpaid. It is a more useful fear.
}
~ learn("houses")
-> topics

= trade_tips
{ hostile():
    Factor Venn: Market advice is a commodity. You are not currently shopping in a manner I enjoy.
    ~ shift(-2)
    -> topics
}
{ trade_tips > 1:
    Factor Venn: Artifacts, Captain. Records. Old things. And weapons, if you meet the Varkesh. My advice has not changed; only its price has.
    -> topics
}
Factor Venn: Minerals, of course. Everyone sells us minerals. We buy them with a sigh and sell them with a song.
Factor Venn: What we truly want are things nobody else has. Artifacts. Records. Anything old that the ground has been keeping secret. For those we pay generously, and I do not use that word lightly. I have never used it before.
{ comms >= 120:
    Comms Officer: He's telling the truth about artifacts. His throat sac just went bright gold.
}
Factor Venn: And if you meet the Varkesh, sell them anything that explodes. They are our best customers.
~ learn("artifacts")
~ learn("varkesh")
-> topics

= survey_offer
~ survey_price = quote(500)
Factor Venn: Charts! Yes. We have survey data on every system our ships have passed, which is all of them, eventually.
Factor Venn: For {survey_price} credits I will give you a full survey of the nearest system you have not yet explored. Every planet scanned, every world named. Your science officer may weep with gratitude.
-> survey_choice

= survey_choice
+ { credits >= survey_price } [Pay {survey_price} credits.]
    -> buy_survey
+ { comms >= 100 && survey_price > 350 } [Haggle.]
    -> haggle_survey
+ [Not today.]
    Factor Venn: Of course. Maxim Four: a customer who walks away is a customer who has not yet walked back.
    -> topics

= haggle_survey
{ survey_price > 500:
    Comms Officer: Factor, you quoted the last captain four hundred. I heard it on the open band.
    Factor Venn: ...You have a very good ear on your crew, Captain. Very well. Four hundred.
    ~ survey_price = 400
- else:
    Comms Officer: That data is months old, Factor. Half of it is on the open band if we listen long enough.
    Factor Venn: Months old! It is vintage. Fine. Three hundred and fifty, and I want it noted that I am smiling through pain.
    ~ survey_price = 350
}
~ shift(1)
-> survey_choice

= buy_survey
{ pay(survey_price):
    ~ temp system = survey_data()
    { system == "":
        Factor Venn: Ah. It seems you have already explored everything we have charted. I return your money, which is a sentence I hate saying.
        ~ receive(survey_price)
    - else:
        Factor Venn: Transmitting now. The {system} system: every world surveyed. Fly safely, and remember where you bought it.
        ~ shift(3)
    }
- else:
    Factor Venn: Your account says otherwise, Captain. Come back with more credits. Or fewer opinions.
}
-> topics

= history_offer
~ history_price = quote(300)
Factor Venn: Ruins. Yes. Maxim One, the oldest one: everything has a price, including the truth.
Factor Venn: Especially the truth.
Factor Venn: Our archive houses keep records older than some of your stars. I can sell you a copy of one for {history_price} credits. Which one, I do not choose. The archive chooses. It has a sense of humour.
+ { credits >= history_price } [Buy an archive record for {history_price} credits.]
    { pay(history_price):
        ~ temp title = give_lore("Orrun Combine")
        { title == "":
            Factor Venn: The archive has nothing you do not already own. Remarkable. I refund you, with interest. No, without interest. Old habit.
            ~ receive(history_price)
        - else:
            Factor Venn: "{title}". A fine choice, made by no one. It is in your captain's log.
            ~ shift(2)
            ~ learn("archive")
        }
    }
    -> topics
+ [Maybe later.]
    -> topics

= first_watch
{ first_watch > 1 && reputation < 20:
    Factor Venn: Still a new customer, Captain. Still an old ledger.
    -> topics
}
Factor Venn's throat sac goes completely grey. All four of his hands go still.
Factor Venn: The First Watch. A name from a very old ledger, Captain. A human name, I think.
{ reputation >= 20:
    Factor Venn: I will tell you this much for free, which should frighten you: that account was never closed. Ask on our homeworld, if the Council will see you. They keep the oldest books.
    ~ learn("council")
- else:
    Factor Venn: And old ledgers are not discussed with new customers. Trade with us a while. Become less new.
}
-> topics

= varkesh
{ varkesh > 1:
    Factor Venn: Loyal, loud, and always buying. If you visit them, do not flatter. They take it as a sign you are hiding a knife.
    -> topics
}
Factor Venn: The Varkesh! Excellent customers. They buy weapons, and then they need repairs, and then they need more weapons. A beautiful cycle.
Factor Venn: They fight because they swore an oath to fight. Against what, none of them can quite say. Something that never came. They are very loyal to it.
-> topics

= recap
{ not any_news():
    Factor Venn: News? Prices are prices, the lanes are open, and nobody important has died. Dull times, Captain. I miss nothing about exciting ones.
    -> topics
}
Factor Venn: News is cheaper than charts, so here is the short version.
{ heard("stage-5"):
    Factor Venn: Your home space is sealed. Nothing goes in or out.
- else:
    { heard("stage-4"):
        Factor Venn: The galaxy is in pieces. Half my debtors cannot be reached, which is the only good thing about it.
    - else:
        { heard("stage-3"):
            Factor Venn: The main lanes are failing. Fuel is dear and refugees are everywhere.
        - else:
            { heard("stage-2"):
                Factor Venn: The rim has gone dark. Ships go out and do not come back.
            }
        }
    }
}
{ heard("first-anchor"):
    Factor Venn: Something sits on the dead lanes, waiting. Nobody who went to look has come back to describe it.
}
{ heard("mother-signal"):
    Factor Venn: And the voice on every band still says the same three words.
}
{ heard("first-artifact"):
    Factor Venn: Also, a certain captain is digging up very old things. My offer for them stands.
}
-> topics

= seal_venn
Factor Venn's throat sac drains from gold to a startled grey.
Factor Venn: Put that away. Slowly. Do you know what that is? No, of course you do not, or you would be holding it with both hands.
Factor Venn: That is the Founding Seal of the Ledger. The Council lost it three hundred years ago and has been pretending ever since that they did not.
Factor Venn: I can buy it from you. Nine thousand credits, which is robbery, but I would be the one going to prison for it. Or you can take it to the Ledger Council yourself. A debt owed by the Council is worth more than money, Captain. Maxim One.
+ [Sell it to Venn for 9,000 credits.]
    { give_artifact("founding_seal"):
        ~ receive(9000)
        ~ shift(5)
        Factor Venn: A pleasure. A terrifying pleasure. Speak of this to no one, and I will speak of you to everyone, warmly.
    }
    -> topics
+ [I'll take it to the Ledger Council.]
    Factor Venn: Wise. Expensive for me, but wise. Go to the homeworld and tell the clerk Factor Venn sent you with "the book's own mark". They will open the door.
    ~ learn("council")
    ~ shift(3)
    -> topics

= goodbye
{ hostile():
    Factor Venn: Leaving already? I will bill you for the silence.
- else:
    Factor Venn: Safe travels, Captain. Maxim Nine: a living customer is worth more than a dead one, unless they owe you nothing.
}
-> END

// ------------------------------------------------------------------ the homeworld

=== homeworld ===
{ reputation <= -20:
    A recorded voice: "The Ledger Council does not take calls from accounts in arrears." The recording repeats, then stops.
    -> END
}
{ not knows("council"):
    A junior clerk appears, mostly forehead and paperwork.
    Clerk Mibb: The Ledger Council does not receive walk-ins, or fly-ins. Make an appointment through a factor. Factors are on the starport. The starport is where you came from.
    { hostile():
        Clerk Mibb: Shouting does not count as an appointment. I have checked.
        ~ shift(-3)
    }
    -> END
}
{ homeworld > 1:
    Councillor Thessaly: You have returned. The books are still open, Captain.
    -> council_news ->
    -> council_topics
}
Five Orrun in heavy formal robes look down from the screen. The one in the centre has eyes the colour of old brass.
Councillor Thessaly: Captain. Factor Venn says you asked about the First Watch. Factor Venn sells information for a living, so we assume he charged you for the question.
Councillor Thessaly: He did not? Then he believes it matters. So do we.
-> council_news ->
-> council_topics

= council_topics
+ [What was the First Watch?] -> watch_story
+ { watch_story } [What is the debt?] -> the_debt
+ { has_artifact("founding_seal") && not seal_council } [Offer the Founding Seal of the Ledger.] -> seal_council
+ [Thank you, Councillor.] -> farewell

= watch_story
Councillor Thessaly: Long ago, before your settlements, ships came from the dark between galaxies. They were human, though they did not call themselves that yet.
Councillor Thessaly: They were frightened. They bought water from us, and seed stock, and one other thing. They bought our silence.
{ comms >= 150:
    Comms Officer: (quietly) Captain, the word she used for "frightened" is the one they use for prey animals.
}
Councillor Thessaly: We have kept it ever since. You are, I think, the first of them to ask.
~ learn("silence")
-> council_topics

= the_debt
Councillor Thessaly: The silence was never paid for. The ledger says only: "to be paid in full, later."
Councillor Thessaly: The Combine does not forget a debt, Captain. But we are patient, and you are not the one who incurred it. Bring us something worth the price, and we will speak of what your ancestors were running from.
{ hostile():
    Councillor Thessaly: And do not raise your voice in this chamber again. We have sold louder ships than yours.
    ~ shift(-5)
- else:
    ~ shift(5)
}
~ learn("the-debt")
-> council_topics

= seal_council
The five councillors rise together. Nobody speaks for a long moment.
Councillor Thessaly: The book's own mark. We had stopped believing it still existed.
{ not the_debt:
    Councillor Thessaly: Your ancestors left a debt in our ledger, Captain: our silence, never paid for. This seal is worth that silence twice over.
}
Councillor Thessaly: We accept it as payment in full. And so the silence is ended.
+ [Hand over the seal.]
    { give_artifact("founding_seal"):
        ~ shift(25)
        ~ learn("the-debt")
        Councillor Thessaly: Hear it, then. The ships from the dark were running from something they had made themselves.
        Councillor Thessaly: They would not name it. They said only that it was patient, that it would never stop looking, and that if it ever found this galaxy, we should close every door we had.
        Councillor Thessaly: We laughed, Captain. Orrun do not close doors. Doors are where customers come from.
        ~ learn("they-made-it")
        { comms >= 120:
            Comms Officer: (quietly) Captain, the verb for "made" is the one they use for children, not for tools.
        }
        ~ give_lore("Orrun Combine")
    - else:
        Councillor Thessaly: You hesitate. Keep it, then, until you are sure.
    }
    -> council_topics
+ [Not yet.]
    Councillor Thessaly: The ledger is patient. It has had practice.
    -> council_topics

= council_news
{ heard("mother-signal") && not council_signal:
    -> council_signal ->
}
->->

= council_signal
Councillor Thessaly: You heard the voice. "Return to me." Every world in the Combine heard it at the same moment.
Councillor Thessaly: Our oldest ledger has that phrase in it, Captain. It is written in the margin of the First Watch's account, in their own hand. Under it, one more word: "Never."
~ learn("return-to-me")
->->

= farewell
Councillor Thessaly: The books remain open. Go carefully. Something else is reading them too.
-> END

// ------------------------------------------------------------------ world events
// The Orrun comment on each significant event once, the first time we talk after news reaches them.
// Events come from the spreadsheet's Events tab; the game fires them as the story unfolds.

=== function any_news() ===
~ return heard("stage-2") || heard("first-anchor") || heard("mother-signal") || heard("first-artifact")

=== news ===
{ heard("mother-signal") && not mother_signal:
    -> mother_signal ->
}
{ heard("first-anchor") && not first_anchor:
    -> first_anchor ->
}
// Only the latest threat stage the Orrun have heard of gets a comment.
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
Factor Venn: Before business, Captain: did you hear it? Every receiver in Brightmarket picked it up at the same moment.
Factor Venn: "Return to me." Three words, on every band. We tried to trace it and found nothing. We tried to sell it and found no buyers. Both are firsts.
{ comms >= 150:
    Comms Officer: Captain... it wasn't in their language when it arrived. It was in ours. A very old form of ours.
}
~ learn("mother-signal")
->->

= first_anchor
Factor Venn: A house ship came back from the outer lanes this morning. Half of it did.
Factor Venn: The crew says the lane simply stopped, and at the place where it stopped there was a station. Black, quiet, and very patient. It did not hail them. It did not need to.
~ learn("anchor")
->->

= stage2
{ days_since("stage-2") > 30:
    Factor Venn: You will have heard by now. The rim houses went silent a month ago and they are still silent.
- else:
    Factor Venn: Captain, a strange week. Three of our rim houses stopped answering. Not bankrupt. Not at war. Just gone quiet.
}
Factor Venn: Ships that leave for the rim do not arrive. The lanes that should carry them are simply not there. The Combine has declared the rim a temporary loss. "Temporary" is a word we use to keep from panicking.
~ learn("rim-dark")
->->

= stage3
Factor Venn: The major lanes are failing, Captain. Fuel costs what drive crystal costs in a famine, and the docks are full of ships with nowhere to go.
Factor Venn: Maxim Thirty: never sell a lifeboat at a discount. This week I am breaking Maxim Thirty. Do not tell anyone. It would ruin me.
~ shift(2)
->->

= stage4
Factor Venn: The galaxy has become several smaller galaxies, and none of them are speaking to each other. Our ledgers no longer balance, because half our debtors cannot be reached.
Factor Venn: I find I am not even angry about the money. That is how I know it is serious.
->->

= stage5
Factor Venn: Captain. Your home. We heard.
Factor Venn: Every lane into human space closed in the same hour. No one goes in. No one comes out.
Factor Venn: I am sorry. I have never said that for free before. I find I do not want to charge you.
~ shift(5)
->->

= first_artifact
Factor Venn: Word travels, Captain. They say you pulled something out of the ground that nobody has known how to make for a very, very long time.
Factor Venn: Whatever price you are imagining for it, I am imagining a larger one. Bring it here before you show it to anyone with a bigger smile than mine.
->->

// ------------------------------------------------------------------ ship encounters
// An Orrun trader met in space. Traders never start fights; they run, pay, or haggle their way out.

=== encounter ===
{ in_combat:
    -> ransom
}
A cramped bridge stacked with crates. A four-armed Orrun waves all four hands at once.
Trader Pell: Greetings, greetings! The freighter Ninth Margin, under contract to House Venn. We carry nothing worth your time, and at very reasonable prices.
{ hostile():
    Trader Pell: Ah. That tone. Maxim Twenty: never argue with a gun you cannot outrun. What do you want?
}
-> trader_talk

= trader_talk
+ [Safe travels.]
    Trader Pell: And to you! Brightmarket has the best prices this side of the rim. I am contractually obliged to say that.
    -> END
+ [Any news from the lanes?]
    { heard("stage-2"):
        Trader Pell: The rim lanes are gone, Captain. Not closed. Gone. Three of our convoys went out and none came back. We take the long way now, and we charge for it.
    - else:
        Trader Pell: Quiet. Suspiciously quiet. Quiet is bad for prices.
    }
    -> trader_talk
+ [Hand over your cargo.]
    -> shakedown

= shakedown
~ crew_reacts("Unprovoked")
{ odds >= 150:
    Trader Pell: ...Very well. Take it. I will be filing a complaint. With everyone. In writing.
    ~ receive(400)
    ~ shift(-15)
    ~ stand_down()
- else:
    Trader Pell: Then we will have to disagree. Loudly. Shields up!
    ~ shift(-10)
    ~ attack()
}
-> END

= ransom
{ odds >= 100:
    Trader Pell: Ceasefire! Ceasefire! Five hundred credits and we never saw each other. I will even throw in a receipt.
    + [Accept the five hundred.]
        ~ receive(500)
        ~ shift(-5)
        ~ crew_reacts("Mercy")
        ~ stand_down()
        Trader Pell: A pleasure doing business. Mostly.
        -> END
    + [No deal.]
        Trader Pell: Then we run!
        -> END
- else:
    Trader Pell: You are losing, Captain, and so am I. Maxim Thirty-One: two losers make one very expensive afternoon. Shall we both stop?
    + [Agreed. Break off.]
        ~ stand_down()
        Trader Pell: Wise. Very wise. I may even forget this happened. For a small fee. Joking!
        -> END
    + [Keep fighting.]
        Trader Pell: Then may your insurance be better than mine.
        -> END
}

// ------------------------------------------------------------------ a settlement on the Orrun world
// Unnamed locals, met by driving the rover into town. Trading is done on the game's market screen.

=== settlement ===
{ reputation <= -40:
    Stall-keeper: Closed accounts get closed shutters, sky customer. Good day.
    -> END
}
{ settlement == 1:
    A crowded market square, every stall roofed in a different colour. A plump Orrun with an abacus in each of three hands spots your rover and waves the fourth.
    Stall-keeper: A rover! From orbit! Customers who come down from the sky always have the most interesting things to sell, and the least idea what they are worth.
- else:
    Stall-keeper: The sky customer returns! I kept your corner of the square warm. Figuratively. Heating is extra.
}
{
- fawning():
    Stall-keeper: Such manners. You will do well here, or at least be robbed politely.
    ~ shift(1)
- hostile():
    Stall-keeper: No need to glare. Glaring is free; everything else has a price.
    ~ shift(-2)
}
-> town_talk

= town_talk
+ [How is business in town?] -> town_mood
+ [Do you buy artifacts?] -> town_artifacts
+ { has_artifact("founding_seal") && not town_seal } [Do you recognise this seal?] -> town_seal
+ [Goodbye.] -> town_bye

= town_mood
{
- heard("stage-4"):
    Stall-keeper: Half the stalls are shuttered. The lanes are cut and the caravans do not come. We sell to each other now, which is like eating your own coat.
- heard("stage-3"):
    Stall-keeper: Prices go up every week. Fuel most of all. The factors say it is the failing lanes. The factors say a lot of things, at a fee.
- heard("stage-2"):
    Stall-keeper: Nervous. The rim houses stopped answering their mail. An Orrun who stops answering mail is either dead or bankrupt, and we are not sure which is worse.
- else:
    Stall-keeper: Good! Busy! Too busy to be talking to you, which is why I am charging for it. That was a joke. Probably.
}
{ heard("first-artifact"):
    Stall-keeper: And everyone is digging in old ruins since word got round that a sky ship found something real. Mostly they find old pots. I sell the pots.
}
-> town_talk

= town_artifacts
Stall-keeper: Little curios, yes, at little prices. A real relic? Take it up to the factors at Brightmarket, who pay properly. Or to the Noeti, who pay improperly well.
Stall-keeper: But if you ever find anything with columns of tiny numbers carved into it, come straight to me. I will faint, and then I will pay.
-> town_talk

= town_seal
The stall-keeper looks at the seal and sits down very suddenly on a crate of melons.
Stall-keeper: That is the Founding Seal of the Ledger. Put it away! I cannot afford to have seen it.
Stall-keeper: It belongs to the Ledger Council. Take it to them, not to me. Tell their clerk it is "the book's own mark", and the door will open.
~ learn("council")
-> town_talk

= town_bye
Stall-keeper: Come back with full holds and empty pockets, sky customer!
-> END
