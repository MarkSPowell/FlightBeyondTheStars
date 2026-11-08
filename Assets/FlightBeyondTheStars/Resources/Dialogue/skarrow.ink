// Flight Beyond the Stars - Skarrow dialogue
// Entry points (chosen by the game): encounter
//
// Quick, funny, nervous scavengers living by "finders' law". Friendly posture works best; a hostile
// posture makes them run (and spread rumours). Calling them thieves is the worst insult there is.
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

VAR posture = "friendly"
VAR comms = 60
VAR credits = 0
VAR reputation = 0
VAR in_combat = false
VAR odds = 100
VAR they_hailed = false

-> encounter

=== function shift(delta) ===
~ reputation = rep(delta)

=== function hostile() ===
~ return posture == "hostile"

// ------------------------------------------------------------------ ship encounters

=== encounter ===
{ in_combat:
    -> truce
}
A cockpit made of six other cockpits, bolted together. A small, big-eyed Skarrow grins at you, tail twitching.
Skipper Nix: Hey hey hey! Big ship! Nice ship! Finders' law says anything floating free is ours, and your cargo looks very, very free.
Skipper Nix: Not stealing! Finding early. Small fee, two hundred credits, and we all go home happy.
{ hostile() && odds >= 80:
    Skipper Nix: ...Okay, that is a lot of guns and a very angry face. You know what, we just remembered we are late for something. Bye!
    ~ shift(-3)
    ~ stand_down()
    -> END
}
-> demand

= demand
+ { credits >= 200 } [Pay the two hundred.]
    { pay(200):
        Skipper Nix: Pleasure! Real pleasure! Fly safe, big ship. Tell your friends about us. Nice things only.
        ~ shift(2)
        ~ stand_down()
    }
    -> END
+ [You're thieves.]
    Skipper Nix: THIEVES? Did you hear that? He called us thieves!
    Skipper Nix: That's it. Weapons hot. Very hot. Hottest they go.
    ~ shift(-5)
    ~ attack()
    -> END
+ [We have nothing worth taking.]
    { comms >= 100:
        Comms Officer: Captain, their scanner is a cooking timer with lights glued to it. They can't see into our hold.
        Skipper Nix: ...Rude. Accurate, but rude. Fine! Nothing worth taking. We knew that. We were testing you.
        ~ stand_down()
        -> END
    - else:
        Skipper Nix: Everybody says that! Scanning... nope, can't tell. Fee's still two hundred.
        -> demand
    }
+ [Back off, or we open fire.]
    { odds >= 100:
        Skipper Nix: Okay! Backing off! Backing! See? This is us, backing.
        ~ shift(-1)
        ~ stand_down()
    - else:
        Skipper Nix: There's more of us than there are of you! I did the maths! Twice!
        ~ attack()
    }
    -> END

= truce
Skipper Nix: Oh good, you're calling. We love calling. Is this a truce call? Please say it's a truce call.
+ [Leave now and we'll let you go.]
    { odds >= 60:
        Skipper Nix: Leaving! Already left! You never saw us!
        ~ stand_down()
    - else:
        Skipper Nix: Hmm. You're looking a bit... broken. Truce cancelled. Sorry! Not sorry. A bit sorry.
    }
+ [Never mind.]
    Skipper Nix: Okay! Back to shooting, then.
- -> END
