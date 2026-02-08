/*
 * - Edited by PrzemyslawNowaczyk (11.10.17)
 *   -----------------------------
 *   Deleting unused variables
 *   Changing obsolete methods
 *   Changing used input methods for consistency
 *   -----------------------------
 *
 * - Edited by NovaSurfer (31.01.17).
 *   -----------------------------
 *   Rewriting from JS to C#
 *   Deleting "Spawn" and "Explode" methods, deleting unused varibles
 *   -----------------------------
 * Just some side notes here.
 *
 * - Should keep in mind that idTech's cartisian plane is different to Unity's:
 *    Z axis in idTech is "up/down" but in Unity Z is the local equivalent to
 *    "forward/backward" and Y in Unity is considered "up/down".
 *
 * - Code's mostly ported on a 1 to 1 basis, so some naming convensions are a
 *   bit fucked up right now.
 *
 * - UPS is measured in Unity units, the idTech units DO NOT scale right now.
 *
 * - Default values are accurate and emulates Quake 3's feel with CPM(A) physics.
 *
 */

using Godot;
using System;

// Contains the command the user wishes upon the character
struct Cmd
{
	public float forwardMove;
	public float rightMove;
	public float upMove;
}

public partial class CPMPlayer : CharacterBody3D
{
	public Transform3D playerView;     // Camera
	public float playerViewYOffset = 0.6f; // The height at which the camera is bound to
	public float xMouseSensitivity = 0.3f;
	public float yMouseSensitivity = 0.3f;
//
	/*Frame occuring factors*/
	public float gravity = 500.0f;

	public float friction = 12; //Ground friction

	/* Movement stuff */
	public float moveSpeed = 700.0f;                // Ground move speed
	public float runAcceleration = 14.0f;         // Ground accel
	public float runDeacceleration = 10.0f;       // Deacceleration that occurs when running on the ground
	public float airAcceleration = 40.0f;          // Air accel
	public float airDecceleration = 40.0f;         // Deacceleration experienced when ooposite strafing
	public float airControl = 0.3f;               // How precise air control is
	public float sideStrafeAcceleration = 100.0f;  // How fast acceleration occurs to get up to sideStrafeSpeed when
	public float sideStrafeSpeed = 100.0f;          // What the max speed to generate when side strafing
	public float jumpSpeed = 300.0f;                // The speed at which the character's up axis gains when hitting jump
	public bool holdJumpToBhop = false;           // When enabled allows player to just hold jump button to keep on bhopping perfectly. Beware: smells like casual.


	private CharacterBody3D _controller;

	// Camera rotations
	private float rotX = 0.0f;
	private float rotY = 0.0f;

	private Vector3 moveDirectionNorm = new Vector3(0,0,0); // Vector3.Zero
	private Vector3 playerVelocity = new Vector3(0,0,0); // Vector3.Zero
	private float playerTopVelocity = 0.0f;

	// Q3: players can queue the next jump just before he hits the ground
	private bool wishJump = false;

	// Used to display real time fricton values
	private float playerFriction = 0.0f;

	// Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
	private Cmd _cmd;

	public override void _Ready()
	{
		// Hide the cursor
		Input.MouseMode = Input.MouseModeEnum.Captured;
		//Cursor.visible = false; // double check it's not visible
		//Cursor.lockState = CursorLockMode.Locked;

		playerView = GetViewport().GetCamera3D().Transform;

		// Put the camera inside the capsule collider
		playerView.Origin = new Vector3(
			this.Transform.Origin.X,
			this.Transform.Origin.Y + playerViewYOffset,
			this.Transform.Origin.Z);

		_controller = this;
	}

	public override void _Input(InputEvent @event)
	{
		if(@event is InputEventMouseMotion){
			InputEventMouseMotion moveEvent = (InputEventMouseMotion)@event;
			/* Camera rotation stuff, mouse controls this shit */
			rotX -= moveEvent.Relative.Y * xMouseSensitivity * 0.02f;
			rotY -= moveEvent.Relative.X * yMouseSensitivity * 0.02f;

			// Clamp the X rotation
			if (rotX < -90)
				rotX = -90;
			else if (rotX > 90)
				rotX = 90;

			this.Rotation = Quaternion.FromEuler(new Vector3(rotX, rotY, 0)).GetEuler(); // Rotates the collider
			// playerView. = Quaternion.FromEuler(new Vector3(rotX, rotY, 0)).GetEuler(); // Rotates the camera

		}
	}


	public override void _Process(double delta)
	{

		/* Ensure that the cursor is locked into the screen */
		if (Input.MouseMode != Input.MouseModeEnum.Captured)
		{
			if (Input.IsMouseButtonPressed(MouseButton.Left))
				Input.MouseMode = Input.MouseModeEnum.Captured;
		}


		/* Movement, here's the important part */
		QueueJump();
		if (_controller.IsOnFloor())
			GroundMove();
		else if (!_controller.IsOnFloor())
			AirMove();

		// Move the controller
		_controller.Velocity = playerVelocity * (float)GetProcessDeltaTime();
		_controller.MoveAndSlide();

		/* Calculate top velocity */
		Vector3 udp = playerVelocity;
		udp.Y = 0.0f;
		if (udp.Length() > playerTopVelocity)
			playerTopVelocity = udp.Length();

		//Need to move the camera after the player has been moved because otherwise the camera will clip the player if going fast enough and will always be 1 frame behind.
		// Set the camera's position to the transform
		// playerView.position = new Vector3(
		// 	transform.position.X,
		// 	transform.position.Y + playerViewYOffset,
		// 	transform.position.Z);
	}

	 /*******************************************************************************************************\
	|* MOVEMENT
	\*******************************************************************************************************/

	/**
	 * Sets the movement direction based on player input
	 */
	private void SetMovementDir()
	{
		_cmd.forwardMove = Input.GetAxis("ui_up", "ui_down");
		_cmd.rightMove = Input.GetAxis("ui_left", "ui_right");
	}

	/**
	 * Queues the next jump just like in Q3
	 */
	private void QueueJump()
	{
		if(holdJumpToBhop)
		{
			wishJump = Input.IsActionPressed("ui_accept");
			return;
		}

		if(Input.IsActionPressed("ui_accept") && !wishJump)
			wishJump = true;
		if(Input.IsActionJustReleased("ui_accept"))
			wishJump = false;
	}

	/**
	 * Execs when the player is in the air
	*/
	private void AirMove()
	{
		Vector3 wishdir;
		float wishvel = airAcceleration;
		float accel;
		
		SetMovementDir();

		wishdir = (Transform.Basis.Z * _cmd.forwardMove) + (Transform.Basis.X * _cmd.rightMove);
		wishdir = wishdir.Normalized();


		float wishspeed = wishdir.Length();
		wishspeed *= moveSpeed;

		moveDirectionNorm = wishdir;

		// CPM: Aircontrol
		float wishspeed2 = wishspeed;
		if (playerVelocity.Dot(wishdir) < 0)
			accel = airDecceleration;
		else
			accel = airAcceleration;
		// If the player is ONLY strafing left or right
		if(_cmd.forwardMove == 0 && _cmd.rightMove != 0)
		{
			if(wishspeed > sideStrafeSpeed)
				wishspeed = sideStrafeSpeed;
			accel = sideStrafeAcceleration;
		}

		Accelerate(wishdir, wishspeed, accel);
		if(airControl > 0)
			AirControl(wishdir, wishspeed2);
		// !CPM: Aircontrol

		// Apply gravity
		playerVelocity.Y -= gravity * (float)GetProcessDeltaTime();
	}

	/**
	 * Air control occurs when the player is in the air, it allows
	 * players to move side to side much faster rather than being
	 * 'sluggish' when it comes to cornering.
	 */
	private void AirControl(Vector3 wishdir, float wishspeed)
	{
		float zspeed;
		float speed;
		float dot;
		float k;

		// Can't control movement if not moving forward or backward
		if(Mathf.Abs(_cmd.forwardMove) < 0.001 || Mathf.Abs(wishspeed) < 0.001)
			return;
		zspeed = playerVelocity.Y;
		playerVelocity.Y = 0;
		/* Next two lines are equivalent to idTech's VectorNormalize() */
		speed = playerVelocity.Length();
		playerVelocity = playerVelocity.Normalized();

		dot = playerVelocity.Dot(wishdir);
		k = 32;
		k *= airControl * dot * dot * (float)GetProcessDeltaTime();

		// Change direction while slowing down
		if (dot > 0)
		{
			playerVelocity.X = playerVelocity.X * speed + wishdir.X * k;
			playerVelocity.Y = playerVelocity.Y * speed + wishdir.Y * k;
			playerVelocity.Z = playerVelocity.Z * speed + wishdir.Z * k;

			playerVelocity = playerVelocity.Normalized();
			moveDirectionNorm = playerVelocity;
		}

		playerVelocity.X *= speed;
		playerVelocity.Y = zspeed; // Note this line
		playerVelocity.Z *= speed;
	}

	/**
	 * Called every frame when the engine detects that the player is on the ground
	 */
	private void GroundMove()
	{
		Vector3 wishdir;

		// Do not apply friction if the player is queueing up the next jump
		if (!wishJump)
			ApplyFriction(1.0f);
		else
			ApplyFriction(0);

		SetMovementDir();

		wishdir = (Transform.Basis.Z * _cmd.forwardMove) + (Transform.Basis.X * _cmd.rightMove);


		wishdir = wishdir.Normalized();
		moveDirectionNorm = wishdir;

		var wishspeed = wishdir.Length();
		wishspeed *= moveSpeed;

		Accelerate(wishdir, wishspeed, runAcceleration);

		// Reset the gravity velocity
		playerVelocity.Y = -gravity * (float)GetProcessDeltaTime();

		if(wishJump)
		{
			playerVelocity.Y = jumpSpeed;
			wishJump = false;
		}
	}

	/**
	 * Applies friction to the player, called in both the air and on the ground
	 */
	private void ApplyFriction(float amount)
	{
		Vector3 vec = playerVelocity; // Equivalent to: VectorCopy();
		float speed;
		float newspeed;
		float control;
		float drop;

		vec.Y = 0.0f;
		speed = vec.Length();
		drop = 0.0f;

		/* Only if the player is on the ground then apply friction */
		if(_controller.IsOnFloor())
		{
			control = speed < runDeacceleration ? runDeacceleration : speed;
			drop = control * friction * (float)GetProcessDeltaTime() * amount;
		}

		newspeed = speed - drop;
		playerFriction = newspeed;
		if(newspeed < 0)
			newspeed = 0;
		if(speed > 0)
			newspeed /= speed;

		playerVelocity.X *= newspeed;
		playerVelocity.Z *= newspeed;
	}

	private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
	{
		float addspeed;
		float accelspeed;
		float currentspeed;

		currentspeed = playerVelocity.Dot(wishdir);
		addspeed = wishspeed - currentspeed;
		if(addspeed <= 0)
			return;
		accelspeed = accel * (float)GetProcessDeltaTime() * wishspeed;
		if(accelspeed > addspeed)
			accelspeed = addspeed;

		playerVelocity.X += accelspeed * wishdir.X;
		playerVelocity.Z += accelspeed * wishdir.Z;
	}

}
