module Program.Index

open Giraffe.GiraffeViewEngine

let private page content = 
    let header = 
        head [] [
            title [] [ encodedText "EventCalendar" ]
            meta [ _charset "utf-8" ]
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
    html [] [
        header
        body [ _style "padding: 100px;" ] [
            div [ _id "elmish-app" ] [ content ]
            script [ _src "/vendors.js" ] []
            script [ _src "/app.js" ] []
        ]
]

let indexView userInfo =
    let content = 
        div [] [
            h1 [] [ str "AzureAd test" ]
            h2 [] [ str ("User: " + userInfo) ] 
            ol [] [
                li [] [ a [ _href "/signin" ] [ str "login" ] ]
                li [] [ a [ _href "/signout" ] [ str "logout" ] ]
            ]
        ]
    page content