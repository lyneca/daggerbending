# DaggerSpell II

## Design Rules

- No buttons
- No magic position setting - physics stuff only
- Gestures
- Make it cool lol
- Dagger is supreme
  - Will help a bunch if using a consistent item/model
  - Maybe also short swords / modded stuff if you're brave

## Orbiting

- Throw daggers softly around you to throw into orbit
- Daggers that aren't held within X radius of you and moving at less than a certain speed are pulled into orbit
  - Thrown daggers have a small cooldown during which they aren't pulled in
- Orbiting daggers **cannot hurt you**
- Orbit is relatively tight, and fairly slow - i.e. a 'slow throw' speed
  - But far enough to not be annoying
- Orbiting daggers attempt to block items
  - Tricky
  - Iterate through all items that are either flying or held by an NPC
  - Don't care about gravity-based trajectory
  - For every object that is close (<3m?) and isFlying or is held
    - Try to move the closest dagger to `(player cylinder collider).ClosestPoint(objectPos)`
    - Physics based move
  - Play a noise to make sure the player notices it
    - And appreciates how hard this is going to be to code
- Important: Orbit is **physics based** and **uses Rigidbody.AddForce**
  - No magic `transform.position = Vector3.Lerp()` allowed
  - `isKinematic = false`

## Imbue

- Imbue collision with enemy: Throws one of your orbiting daggers at the enemy hit
  - Do it after a delay for extra badassery
- Imbued daggers always return after landing or penetrating

## Gestures

- Don't use SpellMergeData - Earthbending-like gesture casts instead
- Gestures have a slight debouncing cooldown (0.5s?)
- Flick hand back (reverse palm direction) to summon dagger
- Spell `.Throw()` gesture
  - If no other gestures fit, throw a dagger
  - Tracking ofc
- Both hands thrown: Throw _all_ daggers in the direction of throw
  - Slighter tracking
- Sweep one hand, palm down
  - Mark enemies that the sweep encounters
- Pull hands together
  - Gather _every_ dagger in the map into orbit
- Pull hands apart
  - Fire every dagger away at a random nearby enemy
- Pull hands up
  - Autospawns X daggers on the ground and imbues them with a random spell
  - Daggers then fly up and join your swarm
- Push hands forward, palms forward
  - Orbiting daggers stop orbiting you and form a shield
  - Shield remains as long as pose is held
- On punch/slap/kick enemy, throw a dagger at 'em
  - why not
  - Do it after a delay for extra badassery
- Cast and grip: Grab the nearest dagger

## Marks

- You can have up to [number of orbiting daggers] marks at once
- Enemies can be marked multiple times
- While enemy is marked:
  - Put some VFX around them idk
  - One dagger per mark fly over and hover above the enemy heads
  - Gesture: index trigger and pull down to throw the daggers downward
  - Marks do not expire while the player is gripping
  - Daggers target chest area (likely to hit the head, otherwise max area covered)

## Merges

- When merging with fire/elec/grav, imbue orbiting daggers one-by-one with the element
- Prioritise unimbued daggers first before imbuing other ones
  - Have an indicator?

## Notes

- Dagger pouch/quiver/bandolier
  - why not
  - Find an asset artist to make them
- Dagger FX
  - Leave a trail of some sort (maybe)



## Tips


