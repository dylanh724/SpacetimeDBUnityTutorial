use spacetimedb::{spacetimedb, Identity, SpacetimeType, ReducerContext};
use log;

// We're using this table as a singleton, so there should typically only be one element where the version is 0.
#[spacetimedb(table)]
#[derive(Clone)]
pub struct Config {
    #[primarykey]
    pub version: u32,
    pub message_of_the_day: String,
}

// This allows us to store 3D points in tables.
#[derive(SpacetimeType, Clone)]
pub struct StdbVector3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

// This stores information related to all entities in our game. In this tutorial
// all entities must at least have an entity_id, a position, a direction and they
// must specify whether or not they are moving.
#[spacetimedb(table)]
#[derive(Clone)]
pub struct EntityComponent {
    #[primarykey]
    // The autoinc macro here just means every time we insert into this table
    // we will receive a new row where this value will be increased by one. This
    // allows us to easily get rows where `entity_id` is unique.
    #[autoinc]
    pub entity_id: u64,
    pub position: StdbVector3,
    pub direction: f32,
    pub moving: bool,
}

// All players have this component and it associates an entity with the user's
// Identity. It also stores their username and whether or not they're logged in.
#[derive(Clone)]
#[spacetimedb(table)]
pub struct PlayerComponent {
    // An entity_id that matches an entity_id in the `EntityComponent` table.
    #[primarykey]
    pub entity_id: u64,
    // The user's identity, which is unique to each player
    #[unique]
    pub owner_id: Identity,
    pub username: String,
    pub logged_in: bool,
}

// This reducer is called when the user logs in for the first time and
// enters a username
#[spacetimedb(reducer)]
pub fn create_player(ctx: ReducerContext, username: String) -> Result<(), String> {
    // Get the Identity of the client who called this reducer
    let owner_id = ctx.sender;

    // Make sure we don't already have a player with this identity
    if PlayerComponent::filter_by_owner_id(&owner_id).is_some() {
        log::info!("Player already exists");
        return Err("Player already exists".to_string());
    }

    // Create a new entity for this player and get a unique `entity_id`.
    let entity_id = EntityComponent::insert(EntityComponent
    {
        entity_id: 0,
        position: StdbVector3 { x: 0.0, y: 0.0, z: 0.0 },
        direction: 0.0,
        moving: false,
    }).expect("Failed to create a unique PlayerComponent.").entity_id;

    // The PlayerComponent uses the same entity_id and stores the identity of
    // the owner, username, and whether or not they are logged in.
    PlayerComponent::insert(PlayerComponent {
        entity_id,
        owner_id,
        username: username.clone(),
        logged_in: true,
    }).expect("Failed to insert player component.");

    log::info!("Player created: {}({})", username, entity_id);

    Ok(())
}

// Called when the module is initially published
#[spacetimedb(init)]
pub fn init() {
    Config::insert(Config {
        version: 0,
        message_of_the_day: "Hello, World!".to_string(),
    }).expect("Failed to insert config.");
}

// Called when the client connects, we update the logged_in state to true
#[spacetimedb(connect)]
pub fn client_connected(ctx: ReducerContext) {
    // called when the client connects, we update the logged_in state to true
    update_player_login_state(ctx, true);
}


// Called when the client disconnects, we update the logged_in state to false
#[spacetimedb(disconnect)]
pub fn client_disconnected(ctx: ReducerContext) {
    // Called when the client disconnects, we update the logged_in state to false
    update_player_login_state(ctx, false);
}

// This helper function gets the PlayerComponent, sets the logged
// in variable and updates the PlayerComponent table row.
pub fn update_player_login_state(ctx: ReducerContext, logged_in: bool) {
    if let Some(player) = PlayerComponent::filter_by_owner_id(&ctx.sender) {
        // We clone the PlayerComponent so we can edit it and pass it back.
        let mut player = player.clone();
        player.logged_in = logged_in;
        PlayerComponent::update_by_entity_id(&player.entity_id.clone(), player);
    }
}

// Updates the position of a player. This is also called when the player stops moving.
#[spacetimedb(reducer)]
pub fn update_player_position(
    ctx: ReducerContext,
    position: StdbVector3,
    direction: f32,
    moving: bool,
) -> Result<(), String> {
    // First, look up the player using the sender identity, then use that
    // entity_id to retrieve and update the EntityComponent
    if let Some(player) = PlayerComponent::filter_by_owner_id(&ctx.sender) {
        if let Some(mut entity) = EntityComponent::filter_by_entity_id(&player.entity_id) {
            entity.position = position;
            entity.direction = direction;
            entity.moving = moving;
            EntityComponent::update_by_entity_id(&player.entity_id, entity);
            return Ok(());
        }
    }

    // If we can not find the PlayerComponent or EntityComponent for
    // this player then something went wrong.
    return Err("Player not found".to_string());
}

#[spacetimedb(table)]
pub struct ChatMessage {
    // The primary key for this table will be auto-incremented
    #[primarykey]
    #[autoinc]
    pub message_id: u64,

    // The entity id of the player that sent the message
    pub sender_id: u64,
    // Message contents
    pub text: String,
}

// Adds a chat entry to the ChatMessage table
#[spacetimedb(reducer)]
pub fn send_chat_message(ctx: ReducerContext, text: String) -> Result<(), String> {
    if let Some(player) = PlayerComponent::filter_by_owner_id(&ctx.sender) {
        // Now that we have the player we can insert the chat message using the player entity id.
        ChatMessage::insert(ChatMessage {
            // this column auto-increments so we can set it to 0
            message_id: 0,
            sender_id: player.entity_id,
            text,
        })
        .unwrap();

        return Ok(());
    }

    Err("Player not found".into())
}


