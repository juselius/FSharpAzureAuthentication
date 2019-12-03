module Program.Index

open Giraffe.GiraffeViewEngine

let private page content =
    let header =
        head [] [
            meta [ _charset "utf-8" ]
            title [] [ encodedText "AzureAd Authentication" ]
            meta [
                _name "viewport"
                _content "width=device-width, initial-scale=1"
            ]
            link [
                _rel "stylesheet"
                _href "https://cdnjs.cloudflare.com/ajax/libs/bulma/0.7.1/css/bulma.min.css"
            ]
            link [
                _rel "stylesheet"
                _href "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css"
            ]
            link [
                _rel "stylesheet"
                _href "https://fonts.googleapis.com/css?family=Open+Sans"
            ]
        ]
    let trunk =
        body [] [
            div [ _id "elmish-app"; _class "container" ] [
                div [ _class "section" ] [
                    content
                ]
            ]
        ]
    html [] [
        header
        trunk
    ]

let indexView userInfo =
    let content =
        div [] [
            h1 [ _class "title is-3" ] [ str "AzureAd test" ]
            div [ _class "box" ] [
                h2 [ _class "title is-5" ] [ str ("User: " + userInfo) ]
                ol [] [
                    li [] [ a [ _href "/signin" ] [ str "Login" ] ]
                    li [] [ a [ _href "/signout" ] [ str "Logout" ] ]
                ]
            ]
        ]
    page content