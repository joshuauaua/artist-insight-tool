# ArtistInsightTool

## Run

```bash

dotnet run -- --data-provider Sqlite --connection-string "Data Source=/Users/joshuang/Desktop/Programming/Ivy/artist-insight-tool/db.sqlite" --seed-database --yes-to-all

```

## Schema

```dbml

Enum revenue_source {
    concert
    sync
    streams
    merch
    other
}

Table artist {
    id int [pk, increment]
    name varchar [not null]
    created_at timestamp
    updated_at timestamp
}

Table album {
    id int [pk, increment]
    artist_id int [not null]
    title varchar [not null]
    release_date date [null]
    created_at timestamp
    updated_at timestamp
}

Table track {
    id int [pk, increment]
    artist_id int [not null]
    album_id int [null]
    title varchar [not null]
    duration int [null]
    created_at timestamp
    updated_at timestamp
}

Table campaign {
    id int [pk, increment]
    artist_id int [not null]
    name varchar [not null]
    start_date date [null]
    end_date date [null]
    created_at timestamp
    updated_at timestamp
}

Table revenue_entry {
    id int [pk, increment]
    artist_id int [not null]
    source revenue_source [not null]
    amount decimal [not null]
    revenue_date date [not null]
    description varchar [null]
    track_id int [null]
    album_id int [null]
    campaign_id int [null]
    created_at timestamp
    updated_at timestamp
}

Ref: album.artist_id > artist.id
Ref: track.artist_id > artist.id
Ref: track.album_id > album.id
Ref: campaign.artist_id > artist.id
Ref: revenue_entry.artist_id > artist.id
Ref: revenue_entry.track_id > track.id
Ref: revenue_entry.album_id > album.id
Ref: revenue_entry.campaign_id > campaign.id

```

## Prompt

```
A tool for artists(musicians) to be able to have an overview of their revenue and income streams, with a central dashboard which gives them an overview of their revenue as well as forecast for future 

They can import csv, pdf, connect to api, and input manual sales (i.e. of merch from shows) 

Revenue can come from concerts, sync, streams, merch, other

They should also be able to tie revenue to specific tracks, albums, campaigns in order to see which generate more revenue, for example

```
