CREATE TABLE users
(
    id UUID PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE friend_requests
(
    id UUID PRIMARY KEY,
    requester_id UUID NOT NULL REFERENCES users(id),
    recipient_id UUID NOT NULL REFERENCES users(id),
    status TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    responded_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE conversations
(
    id UUID PRIMARY KEY,
    kind TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE conversation_members
(
    conversation_id UUID NOT NULL REFERENCES conversations(id),
    user_id UUID NOT NULL REFERENCES users(id),
    joined_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (conversation_id, user_id)
);

CREATE TABLE messages
(
    id UUID PRIMARY KEY,
    conversation_id UUID NOT NULL REFERENCES conversations(id),
    sender_id UUID NOT NULL REFERENCES users(id),
    body TEXT NOT NULL,
    media_url TEXT NOT NULL,
    media_type TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE posts
(
    id UUID PRIMARY KEY,
    author_id UUID NOT NULL REFERENCES users(id),
    caption TEXT NOT NULL,
    media_url TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);
