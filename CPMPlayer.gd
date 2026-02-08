 #
 # - Edited by PrzemyslawNowaczyk (11.10.17)
 #   -----------------------------
 #   Deleting unused variables
 #   Changing obsolete methods
 #   Changing used input methods for consistency
 #   -----------------------------
 #
 # - Edited by NovaSurfer (31.01.17).
 #   -----------------------------
 #   Rewriting from JS to C#
 #   Deleting "Spawn" and "Explode" methods, deleting unused varibles
 #   -----------------------------
 # Just some side notes here.
 #
 # - Should keep in mind that idTech's cartisian plane is different to Unity's:
 #    Z axis in idTech is "up/down" but in Unity Z is the local equivalent to
 #    "forward/backward" and Y in Unity is considered "up/down".
 #
 # - Code's mostly ported on a 1 to 1 basis, so some naming convensions are a
 #   bit fucked up right now.
 #
 # - UPS is measured in Unity units, the idTech units DO NOT scale right now.
 #
 # - Default values are accurate and emulates Quake 3's feel with CPM(A) physics.
 #
 #
extends CharacterBody3D


# Contains the command the user wishes upon the character
class Cmd:
	var forwardMove: float;
	var rightMove: float;
	var upMove: float;


var playerView: Transform3D ;     # Camera
var playerViewYOffset: float = 0.6; # The height at which the camera is bound to
var xMouseSensitivity: float = 0.3;
var yMouseSensitivity: float = 0.3;
#
#Frame occuring factors*/
var gravity: float = 500.0;

var friction: float = 12; #Ground friction

# Movement stuff */
var moveSpeed: float = 700.0;                # Ground move speed
var runAcceleration: float = 14.0;         # Ground accel
var runDeacceleration: float = 10.0;       # Deacceleration that occurs when running on the ground
var airAcceleration: float = 40.0;          # Air accel
var airDecceleration: float = 40.0;         # Deacceleration experienced when ooposite strafing
var airControl: float = 0.3;               # How precise air control is
var sideStrafeAcceleration: float = 100.0;  # How fast acceleration occurs to get up to sideStrafeSpeed when
var sideStrafeSpeed: float = 100.0;          # What the max speed to generate when side strafing
var jumpSpeed: float = 300.0;                # The speed at which the character's up axis gains when hitting jump
var holdJumpToBhop: bool  = false;           # When enabled allows player to just hold jump button to keep on bhopping perfectly. Beware: smells like casual.


# Camera rotations
var rotX: float = 0.0;
var rotY: float = 0.0;

var moveDirectionNorm: Vector3  = Vector3(0,0,0); # Vector3.Zero
var playerVelocity: Vector3  = Vector3(0,0,0); # Vector3.Zero
var playerTopVelocity: float = 0.0;

# Q3: players can queue the next jump just before he hits the ground
var wishJump: bool  = false;

# Used to display real time fricton values
var playerFriction: float = 0.0;

# Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
var _cmd: Cmd = Cmd.new();

func _ready() -> void:
	# Hide the cursor
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	
	playerView = get_viewport().get_camera_3d().transform;
	
	# Put the camera inside the capsule collider
	playerView.origin = Vector3(
		transform.origin.x,
		transform.origin.y + playerViewYOffset,
		transform.origin.z);


func _input(event: InputEvent) -> void:
	if(event is InputEventMouseMotion):
		var moveEvent: InputEventMouseMotion = event;
		# Camera rotation stuff, mouse controls this shit */
		rotX -= moveEvent.relative.y * xMouseSensitivity * 0.02;
		rotY -= moveEvent.relative.x * yMouseSensitivity * 0.02;

		# Clamp the X rotation
		if (rotX < -90):
			rotX = -90;
		elif (rotX > 90):
			rotX = 90;

		rotation = Quaternion.from_euler(Vector3(rotX, rotY, 0)).get_euler(); # Rotates the collider
		# playerView. = Quaternion.FromEuler(Vector3(rotX, rotY, 0)).GetEuler(); # Rotates the camera



func _process(delta: float) -> void:
	# Ensure that the cursor is locked into the screen */
	if (Input.mouse_mode != Input.MOUSE_MODE_CAPTURED):
		if (Input.is_mouse_button_pressed(MouseButton.MOUSE_BUTTON_LEFT)):
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED;


	# Movement, here's the important part */
	QueueJump();
	if (is_on_floor()):
		GroundMove();
	elif (!is_on_floor()):
		AirMove();

	# Move the controller
	velocity = playerVelocity * delta;
	move_and_slide();

	# Calculate top velocity */
	var udp: Vector3  = playerVelocity;
	udp.y = 0.0;
	if (udp.length() > playerTopVelocity):
		playerTopVelocity = udp.length();

	#Need to move the camera after the player has been moved because otherwise the camera will clip the player if going fast enough and will always be 1 frame behind.
	# Set the camera's position to the transform
	# playerView.position = new Vector3(
	# 	transform.position.X,
	# 	transform.position.Y + playerViewYOffset,
	# 	transform.position.Z);


 #******************************************************************************************************\
 # MOVEMENT
 #******************************************************************************************************/

#*
# Sets the movement direction based on player input
#*
func SetMovementDir() -> void:
	_cmd.forwardMove = Input.get_axis("ui_up", "ui_down");
	_cmd.rightMove = Input.get_axis("ui_left", "ui_right");

#*
#* Queues the next jump just like in Q3
#*
func QueueJump() -> void :
	if(holdJumpToBhop):
		wishJump = Input.is_action_pressed("ui_accept");
		return;

	if(Input.is_action_pressed("ui_accept") && !wishJump):
		wishJump = true;
	if(Input.is_action_just_released("ui_accept")):
		wishJump = false;

#*
#* Execs when the player is in the air
#/
func AirMove() -> void:
	var wishdir: Vector3;
	var wishvel: float = airAcceleration;
	var accel: float;
	
	SetMovementDir();

	wishdir = (transform.basis.z * _cmd.forwardMove) + (transform.basis.x * _cmd.rightMove);
	wishdir = wishdir.normalized();


	var wishspeed: float = wishdir.length();
	wishspeed *= moveSpeed;

	moveDirectionNorm = wishdir;

	# CPM: Aircontrol
	var wishspeed2: float = wishspeed;
	if (playerVelocity.dot(wishdir) < 0):
		accel = airDecceleration;
	else:
		accel = airAcceleration;
	# If the player is ONLY strafing left or right
	if(_cmd.forwardMove == 0 && _cmd.rightMove != 0):
		if(wishspeed > sideStrafeSpeed):
			wishspeed = sideStrafeSpeed;
		accel = sideStrafeAcceleration;

	Accelerate(wishdir, wishspeed, accel);
	if(airControl > 0):
		AirControl(wishdir, wishspeed2);
	# !CPM: Aircontrol

	# Apply gravity
	playerVelocity.y -= gravity * get_process_delta_time();

 #*
 #* Air control occurs when the player is in the air, it allows
 #* players to move side to side much faster rather than being
 #* 'sluggish' when it comes to cornering.
 #*
func AirControl(wishdir: Vector3, wishspeed: float ) -> void:
	var zspeed: float;
	var speed: float;
	var dot: float;
	var k: float;

	# Can't control movement if not moving forward or backward
	# if(Mathf.Abs(_cmd.forwardMove) < 0.001 || Mathf.Abs(wishspeed) < 0.001)
	# 	return;
	zspeed = playerVelocity.y;
	playerVelocity.y = 0;
	# Next two lines are equivalent to idTech's VectorNormalize() */
	speed = playerVelocity.length();
	playerVelocity = playerVelocity.normalized();

	dot = playerVelocity.dot(wishdir);
	k = 32;
	k *= airControl * dot * dot * get_process_delta_time();

	# Change direction while slowing down
	if (dot > 0):
		playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
		playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
		playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

		playerVelocity = playerVelocity.normalized();
		moveDirectionNorm = playerVelocity;

	playerVelocity.x *= speed;
	playerVelocity.y = zspeed; # Note this line
	playerVelocity.z *= speed;

#*
#* Called every frame when the engine detects that the player is on the ground
#*
func GroundMove() -> void:
	var wishdir: Vector3;

	# Do not apply friction if the player is queueing up the next jump
	if (!wishJump):
		ApplyFriction(1.0);
	else:
		ApplyFriction(0);

	SetMovementDir();

	wishdir = (transform.basis.z * _cmd.forwardMove) + (transform.basis.x * _cmd.rightMove);


	wishdir = wishdir.normalized();
	moveDirectionNorm = wishdir;

	var wishspeed = wishdir.length();
	wishspeed *= moveSpeed;

	Accelerate(wishdir, wishspeed, runAcceleration);

	# Reset the gravity velocity
	playerVelocity.y = -gravity * get_process_delta_time();

	if(wishJump):
		playerVelocity.y = jumpSpeed;
		wishJump = false;

#*
#* Applies friction to the player, called in both the air and on the ground
#*
func ApplyFriction(amount: float) -> void:
	var vec: Vector3 = playerVelocity; # Equivalent to: VectorCopy();
	var speed: float;
	var newspeed: float;
	var control: float;
	var drop: float;

	vec.y = 0.0;
	speed = vec.length();
	drop = 0.0;

	# Only if the player is on the ground then apply friction */
	if(is_on_floor()):
		control = runDeacceleration if speed < runDeacceleration else speed;
		drop = control * friction * get_process_delta_time() * amount;

	newspeed = speed - drop;
	playerFriction = newspeed;
	if(newspeed < 0):
		newspeed = 0;
	if(speed > 0):
		newspeed /= speed;

	playerVelocity.x *= newspeed;
	playerVelocity.z *= newspeed;

func Accelerate(wishdir: Vector3, wishspeed: float, accel: float):
	var addspeed: float;
	var accelspeed: float;
	var currentspeed: float;

	currentspeed = playerVelocity.dot(wishdir);
	addspeed = wishspeed - currentspeed;
	if(addspeed <= 0):
		return;
	accelspeed = accel * get_process_delta_time() * wishspeed;
	if(accelspeed > addspeed):
		accelspeed = addspeed;

	playerVelocity.x += accelspeed * wishdir.x;
	playerVelocity.z += accelspeed * wishdir.z;
