using Godot;

// PlayerController hallinnoi pelaajan liikkumista ja toimintoja.
// Tämä skripti käsittelee syötteen (näppäimistö/ohjain) ja muuttaa sen liikkeeksi.
// Elinvoima on eriytetty omaan HealthComponent-skriptiin pitämään koodi siistinä.
public partial class PlayerController : CharacterBody3D
{
	// Liikkumisnopeus — muutettavissa editorissa
	[Export] public float Speed = 5.0f;

	// Hyppyvoima — kuinka korkealle pelaaja hyppää
	[Export] public float JumpVelocity = 8.0f;

	// Painovoima — kuinka nopeasti pelaaja putoaa
	[Export] public float Gravity = 20.0f;

	// Viittaus pelaajan näkyvään kapselimeshiin — piilotettu mutta vielä käytössä törmäykseen
	private MeshInstance3D _mesh;

	// Viittaus hahmon 3D-malliin — tarvitaan kääntymistä varten
	private Node3D _characterModel;

	// Viittaus HealthComponentiin — haetaan _Ready:ssä
	private HealthComponent _healthComponent;

	// _Ready ajetaan kun pelaaja ladataan sceneen
	public override void _Ready()
	{
		// Haetaan MeshInstance3D pelaajan lapsista nimellä
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");

		// Haetaan GameOverCharacter-node Playerin lapsista kääntymistä varten
		_characterModel = GetNode<Node3D>("gameover_character");

		// Haetaan HealthComponent pelaajan lapsista nimellä
		_healthComponent = GetNode<HealthComponent>("HealthComponent");

		// Kytketään HealthComponentin signaalit tähän skriptiin
		// Kun elinvoima muuttuu, kutsutaan OnHealthChanged-funktiota
		_healthComponent.HealthChanged += OnHealthChanged;

		// Kun pelaaja kuolee, kutsutaan OnPlayerDied-funktiota
		_healthComponent.PlayerDied += OnPlayerDied;
	}

	// _PhysicsProcess ajetaan joka fysiikka-askel (60 kertaa sekunnissa)
	// delta = aika edellisestä framesta — käytetään tasaiseen liikkeeseen
	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Lisätään painovoima jos pelaaja ei ole maassa
		if (!IsOnFloor())
			velocity.Y -= Gravity * (float)delta;

		// Tarkistetaan hyppysyöte — vain jos pelaaja on maassa
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Haetaan vaaka-liike — palauttaa -1 (vasen), 0 (ei liikettä) tai 1 (oikea)
		float direction = Input.GetAxis("move_left", "move_right");
		velocity.X = direction * Speed;

		// Käännetään hahmo liikkeen mukaan Rotationilla
		// Y = 180 astetta → oikealle, Y = 0 astetta → vasemmalle
		if (direction > 0)
			_characterModel.Rotation = new Vector3(Mathf.DegToRad(-90), Mathf.DegToRad(90), 0);
		else if (direction < 0)
			_characterModel.Rotation = new Vector3(Mathf.DegToRad(-90), Mathf.DegToRad(-90), 0);

		// Testitarkoitus: R-näppäimellä otetaan vahinkoa
		// Tämä poistetaan myöhemmin kun viholliset on tehty
		if (Input.IsActionJustPressed("test_damage"))
			_healthComponent.TakeDamage(1);

		// Asetetaan laskettu nopeus ja liikutetaan pelaajaa
		Velocity = velocity;
		MoveAndSlide();
	}

	// OnHealthChanged kutsutaan kun pelaajan elinvoima muuttuu
	// currentHealth = nykyinen elinvoima, maxHealth = maksimielinvoima
	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		GD.Print($"UI päivitys: {currentHealth}/{maxHealth}");
		// Tähän lisätään myöhemmin UI-päivitys
	}

	// OnPlayerDied kutsutaan kun pelaajan elinvoima menee nollaan
	private void OnPlayerDied()
	{
		GD.Print("Game Over!");
		// Tähän lisätään myöhemmin game over -näyttö ja respawn
	}
}
